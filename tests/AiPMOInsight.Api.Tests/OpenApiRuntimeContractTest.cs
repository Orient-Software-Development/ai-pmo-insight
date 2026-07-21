using AwesomeAssertions;
using NSwag;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// Layer-2 spec-drift sensor: for every GET endpoint declared in the committed openapi.json, actually
/// call it via <see cref="TestWebAppFactory"/> and verify the response body validates against the
/// schema the spec declares for the returned status code. Complements the Layer-1 static shape check
/// (<see cref="OpenApiDriftTest"/>) — Layer 1 fails if the *declared* shape drifts; Layer 2 fails if
/// the *actual runtime response* drifts from the declaration.
///
/// Scope is deliberately narrow: <b>GETs only</b>. POST endpoints need request bodies (login credentials,
/// change-password, ingest multipart, etc.) — generating valid bodies per schema is complex and would
/// duplicate the hand-written <c>*EndpointsTests</c> that already exercise those code paths. Path params
/// use type-aware placeholders (a fixed GUID for <c>format:uuid</c>, a fixed string otherwise) — the
/// intent is contract validation, not fuzzing, so a deterministic call per endpoint is enough.
/// </summary>
public class OpenApiRuntimeContractTest
{
    // Type-aware placeholders for path params. Real data is not seeded in TestWebAppFactory's
    // in-memory database, so most typed-lookup endpoints will return 404 — which is exactly the
    // 404 response the spec must declare (via .Produces(404) on the endpoint) for Layer 2 to pass.
    private const string PlaceholderString = "TEST-KEY";
    private const string PlaceholderUuid = "00000000-0000-0000-0000-000000000000";

    [Fact]
    public async Task Every_GET_endpoint_response_body_matches_the_declared_schema()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClientAs("test-user", "admin", "user");

        var baselinePath = FindBaselinePath();
        var doc = await OpenApiDocument.FromFileAsync(baselinePath);

        var failures = new List<string>();

        foreach (var (pathTemplate, pathItem) in doc.Paths)
        {
            foreach (var (method, op) in pathItem)
            {
                if (!string.Equals(method, OpenApiOperationMethod.Get, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var url = SubstitutePathParams(pathTemplate, op);
                using var response = await client.GetAsync(url);
                var statusCode = ((int)response.StatusCode).ToString();
                var body = await response.Content.ReadAsStringAsync();

                if (!op.Responses.TryGetValue(statusCode, out var declared))
                {
                    failures.Add($"GET {pathTemplate} returned undeclared status {statusCode}");
                    continue;
                }

                // No body declared (empty 404 / 204 / 401) → nothing to validate against.
                if (declared.Content is null
                    || !declared.Content.TryGetValue("application/json", out var mediaType)
                    || mediaType.Schema is null)
                {
                    continue;
                }

                // Empty body against a declared JSON schema counts as a violation — the schema said
                // there would be a body of shape X.
                if (string.IsNullOrEmpty(body))
                {
                    failures.Add($"GET {pathTemplate} → {statusCode} body is empty but schema declares one");
                    continue;
                }

                var errors = mediaType.Schema.Validate(body);
                if (errors.Count > 0)
                {
                    var summary = string.Join("; ", errors.Take(5).Select(e => $"{e.Kind}@{e.Path}"));
                    failures.Add($"GET {pathTemplate} → {statusCode} body: {summary}");
                }
            }
        }

        failures.Should().BeEmpty(
            "the runtime response for every GET endpoint must match the schema its openapi.json " +
            "declares. Failures:\n  " + string.Join("\n  ", failures));
    }

    private static string SubstitutePathParams(string pathTemplate, OpenApiOperation op)
    {
        var url = pathTemplate;
        foreach (var param in op.Parameters.Where(p => p.Kind == OpenApiParameterKind.Path))
        {
            var placeholder = "{" + param.Name + "}";
            var value = string.Equals(param.Schema?.Format, "uuid", StringComparison.OrdinalIgnoreCase)
                ? PlaceholderUuid
                : PlaceholderString;
            url = url.Replace(placeholder, value);
        }
        return url;
    }

    // Same repo-root discovery as OpenApiDriftTest — walk up to the first .git folder.
    private static string FindBaselinePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }
        if (dir is null)
        {
            throw new InvalidOperationException(
                $"Could not locate the repo root (no .git found walking up from {AppContext.BaseDirectory}).");
        }
        return Path.Combine(dir.FullName, "openapi.json");
    }
}
