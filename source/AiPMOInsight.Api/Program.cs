using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using AiPMOInsight.Api.Endpoints;
using AiPMOInsight.Api.Security;
using AiPMOInsight.Application;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.HealthScoring;
using AiPMOInsight.Infrastructure;
using AiPMOInsight.Infrastructure.Persistence;
using AiPMOInsight.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// Logging: stamp every log with the current trace/span id so logs correlate with OTel traces.
// Development keeps the human-readable console; other environments emit structured JSON for
// log aggregators.
builder.Logging.Configure(options =>
    options.ActivityTrackingOptions =
        ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Self-hosted user store: ASP.NET Core Identity core services (UserManager / RoleManager /
// password hashing) over our Postgres. AddIdentityCore registers no auth scheme — JWT (below)
// owns authentication. AddRoles enables RBAC.
builder.Services.AddAuthorization();
builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        // Account lockout (brute-force mitigation): lock for 5 minutes after 5 failed logins.
        // /api/auth/login enforces this via UserManager's lockout methods.
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

// JWT bearer authentication. Settings come from the "Jwt" config section (signing key from a
// secret / env var in production). TokenService issues the tokens; /api/auth/login hands them out.
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Missing 'Jwt' configuration section.");
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddScoped<ITokenService, TokenService>();

// LLM runtime settings (model-swap seam). Inert this slice — only the fake client is registered —
// but bound now so the real adapter next change is a config-only swap. API key via Llm__ApiKey env.
builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection(LlmOptions.SectionName));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claim names verbatim (notably "sub"); we emit standard ClaimTypes URIs already.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            // Pin the signing algorithm — never trust the token's own `alg` (algorithm-confusion
            // defense). Only HS256 tokens are accepted.
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            // Tolerate ±2 min of clock drift between instances (default is 5 min).
            ClockSkew = TimeSpan.FromMinutes(2),
        };

        // The access token rides in an httpOnly cookie, not the Authorization header — pull it from
        // there before validation. Falls back to the header if the cookie is absent.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue(AuthCookies.AccessCookieName, out var token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },
        };
    });

// Surface the caller to the Application layer via ICurrentUser.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Health checks: liveness has no dependencies; readiness pings the database via EF Core
// (CanConnect). Probes hit /health/live and /health/ready (see endpoint mapping below).
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

// OpenTelemetry: traces, metrics, and logs for ASP.NET Core, HttpClient, EF Core, Npgsql, and the
// runtime, exported via OTLP. The exporter honors the standard OTEL_EXPORTER_OTLP_* env vars
// (endpoint defaults to http://localhost:4317); point it at your collector in each environment.
// All three signals share one resource, so a backend (e.g. the LGTM stack in docker-compose)
// correlates a trace with its own logs.
builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: builder.Environment.ApplicationName,
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString()))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        // EF Core span per LINQ query. The 1.16+ instrumentation emits the rendered SQL as the
        // span's db.statement attribute.
        .AddEntityFrameworkCoreInstrumentation()
        // Npgsql-level span for the driver's connection/command timing, complementing the EF span.
        .AddNpgsql()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        // Npgsql's built-in Meter — connection-pool depth, command counts/timing, prepared-statement
        // stats. Subscribing here is enough; no separate AddNpgsqlInstrumentation call is needed.
        .AddMeter("Npgsql")
        .AddOtlpExporter())
    // Route the ILogger pipeline through OTLP too (survives the ClearProviders above, since it is
    // registered here). IncludeFormattedMessage keeps the rendered text; IncludeScopes carries scope
    // values — and the trace/span id stamped above — onto each exported log record.
    .WithLogging(logging => logging.AddOtlpExporter(), options =>
    {
        options.IncludeFormattedMessage = true;
        options.IncludeScopes = true;
    });

builder.Services.AddOpenApi();

var app = builder.Build();

// Apply EF Core migrations + seed RBAC roles (and an optional dev admin) in Development.
// In production run `dotnet ef database update` as a deliberate deploy step instead.
if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateAndSeedAsync();
}

// Health scoring (Phase 4): announce whether the swappable config is still the PRD EXAMPLE
// placeholder set. Loud so no one mistakes the illustrative weights/thresholds/overrides for
// client-agreed numbers (risk R1); replace the 'HealthScoring' section at PMO kickoff.
var healthScoring = app.Services.GetRequiredService<HealthScoringOptions>();
if (healthScoring.IsPlaceholder)
{
    app.Logger.LogWarning(
        "Health scoring is using the EXAMPLE placeholder configuration (weights/thresholds/overrides " +
        "from the PRD). These are illustrative only — replace the 'HealthScoring' section with " +
        "PMO-agreed values before scoring goes live.");
}
else
{
    app.Logger.LogInformation("Health scoring is using a non-placeholder (overridden) configuration.");
}

// Liveness: process is up (no dependency checks). Readiness: dependencies (DB) reachable.
// /health is kept as a liveness alias for back-compat with existing probes.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

// OpenAPI document at /openapi/v1.json (used by the ClientApp's nswag client generator).
app.MapOpenApi();

// Serve the React SPA built into wwwroot (see AiPMOInsight.Api.csproj publish target).
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// Slice endpoints own their route group, tags, and auth policy internally (see each
// Map*Endpoints). Auth: /api/auth/* (httpOnly-cookie tokens). Profile: /api/profile/me.
// Ingest: /api/ingest/upload. Analysis: /api/analyze/{uploadId}. Projects (L2): /api/projects/{projectKey}.
app.MapAuthEndpoints();
app.MapProfileEndpoints();
app.MapIngestEndpoints();
app.MapFindingsEndpoints();
app.MapUploadHistoryEndpoints();
app.MapHealthScoringEndpoints();
app.MapExecutivePortfolioEndpoints();


// SPA client-side routing: serve index.html for unmatched non-API routes.
app.MapFallbackToFile("index.html");

app.Logger.LogInformation("Starting a new instance of the app");
app.Run();

// Exposed so integration tests can reference the entry point via WebApplicationFactory.
public partial class Program;
