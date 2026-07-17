using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Domain.Findings;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiPMOInsight.Infrastructure.Persistence;

/// <summary>
/// Applies pending EF Core migrations and seeds RBAC roles plus an optional development admin
/// user. Call once on startup (typically guarded to Development). Safe to run repeatedly.
/// </summary>
public static class DbInitializer
{
    /// <summary>Roles the app knows about. Extend as needed; referenced by RequireRole(...).</summary>
    public static readonly string[] Roles = ["admin", "user"];

    public static async Task MigrateAndSeedAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        await sp.GetRequiredService<AppDbContext>().Database.MigrateAsync(cancellationToken);

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Optional dev admin: set Seed:Admin:Email + Seed:Admin:Password in configuration
        // (e.g. user-secrets / appsettings.Development.json) to auto-create an admin login.
        var config = sp.GetRequiredService<IConfiguration>();
        var email = config["Seed:Admin:Email"];
        var password = config["Seed:Admin:Password"];
        if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
        {
            var userManager = sp.GetRequiredService<UserManager<AppUser>>();
            if (await userManager.FindByEmailAsync(email) is null)
            {
                var admin = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
                var result = await userManager.CreateAsync(admin, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "admin");
                }
            }
        }

        await SeedProgressDemoAsync(sp, cancellationToken);
    }

    /// <summary>
    /// Development demo data for the Level-2 "this-period progress" panel: a dedicated <c>DEMO-TREND</c>
    /// project seeded with two runs (an earlier, worse one and a later, better one) so the run-over-run
    /// comparison shows real movement without needing two uploads. Idempotent — skipped once the project
    /// already has findings, and never touches real uploaded projects.
    /// </summary>
    private static async Task SeedProgressDemoAsync(IServiceProvider sp, CancellationToken cancellationToken)
    {
        const string key = "DEMO-TREND";
        var findings = sp.GetRequiredService<IFindingRepository>();

        if ((await findings.GetByProjectKeyAsync(key, cancellationToken)).Count > 0)
        {
            return; // already seeded
        }

        var uploadId = Guid.NewGuid();
        var earlier = new DateTimeOffset(2026, 06, 01, 0, 0, 0, TimeSpan.Zero);
        var later = new DateTimeOffset(2026, 07, 01, 0, 0, 0, TimeSpan.Zero);
        var run1 = Guid.NewGuid();
        var run2 = Guid.NewGuid();

        Finding F(Guid run, DateTimeOffset at, HealthArea area, Severity severity, string locator, string summary) =>
            Finding.Create(
                projectKey: key, summary: summary, citation: Citation.Create(uploadId, locator), now: at,
                runId: run, producingAgent: area.ToString(), kind: FindingKind.Analysis,
                confidence: Confidence.High, area: area, severity: severity);

        var seed = new[]
        {
            // Earlier run — worse.
            F(run1, earlier, HealthArea.Schedule, Severity.Red, "Milestones!Cutover", "Milestone 'Cutover' is overdue by 20 days (major delay)."),
            F(run1, earlier, HealthArea.Budget, Severity.Amber, "Budget!Total", "'Total' forecast exceeds budget by 8%."),
            F(run1, earlier, HealthArea.Risk, Severity.Red, "RAID!R1", "Unmitigated critical risk: integration vendor slip."),
            // Later run — better: schedule recovered, risk cleared, a new (smaller) resource issue appears.
            F(run2, later, HealthArea.Schedule, Severity.Green, "Milestones!Cutover", "Milestone 'Cutover' completed on the revised date."),
            F(run2, later, HealthArea.Budget, Severity.Amber, "Budget!Total", "'Total' forecast exceeds budget by 7%."),
            F(run2, later, HealthArea.Resource, Severity.Amber, "Resources!Anna Berg", "Anna Berg is allocated across 3 projects."),
        };

        await findings.AddRangeAsync(seed, cancellationToken);
    }
}
