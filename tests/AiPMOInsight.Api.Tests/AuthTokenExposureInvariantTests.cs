using System.Net.Http.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// Encodes the <b>tokens never in response body</b> invariant from CLAUDE.md §5 Never and
/// CLAUDE-decisions.md ("JWT in httpOnly cookies, not Authorization header"): both access and
/// refresh tokens travel exclusively as <c>httpOnly</c> cookies. Their raw values must never
/// appear in the response payload where JavaScript could read them (XSS mitigation).
///
/// Detected via a regex that matches the HS256 JWT format
/// (<c>base64url.base64url.base64url</c>, with both header and payload starting with <c>eyJ</c>).
/// The pattern is specific enough to avoid false-positives on ordinary JSON responses.
/// A token leaking into the payload — e.g. someone adding <c>Results.Ok(new { access })</c>
/// during a refactor — fires this test loudly with a pointer back at the rule.
/// </summary>
public class AuthTokenExposureInvariantTests
{
    // HS256 JWTs are exactly three base64url segments joined with dots. Header + payload both
    // start with `eyJ` (base64url of `{"`). This won't match arbitrary strings containing dots.
    private static readonly Regex JwtPattern = new(
        @"eyJ[A-Za-z0-9_-]+\.eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+",
        RegexOptions.Compiled);

    private const string InvariantReason =
        "auth invariant (CLAUDE.md §5 Never): both access and refresh tokens travel only as " +
        "httpOnly cookies, never in the response body. If this fails, a token was leaked into " +
        "the JSON payload where JavaScript can read it (XSS-exposed). See CLAUDE-decisions.md " +
        "for the rule.";

    [Fact]
    public async Task Login_response_body_does_not_leak_the_access_token()
    {
        using var factory = new JwtTestFactory();
        await factory.SeedRolesAsync();
        using var client = factory.CreateClient();

        var creds = new { email = "leak-check@example.com", password = "Passw0rd!$" };
        await client.PostAsJsonAsync("/api/auth/register", creds);
        var response = await client.PostAsJsonAsync("/api/auth/login", creds);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();

        JwtPattern.IsMatch(body).Should().BeFalse(InvariantReason);
    }

    [Fact]
    public async Task Refresh_response_body_does_not_leak_the_rotated_access_token()
    {
        using var factory = new JwtTestFactory();
        await factory.SeedRolesAsync();
        using var client = factory.CreateClient();

        var creds = new { email = "leak-check-refresh@example.com", password = "Passw0rd!$" };
        await client.PostAsJsonAsync("/api/auth/register", creds);
        await client.PostAsJsonAsync("/api/auth/login", creds);

        // HttpClient (via WebApplicationFactory) carries the refresh cookie automatically.
        var response = await client.PostAsync("/api/auth/refresh", content: null);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();

        JwtPattern.IsMatch(body).Should().BeFalse(InvariantReason);
    }
}
