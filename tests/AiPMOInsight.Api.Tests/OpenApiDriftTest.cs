using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// Layer-1 spec-drift sensor: the live OpenAPI document served at <c>/openapi/v1.json</c> must
/// match the committed baseline at <c>&lt;repo-root&gt;/openapi.json</c>. Any change to the API
/// surface (added / removed / renamed endpoint, parameter, or DTO field) fails this test so the
/// baseline update is reviewed in the same PR as the code change.
///
/// To accept a deliberate API change, regenerate the baseline and commit the diff:
///     UPDATE_OPENAPI_BASELINE=1 dotnet test --filter FullyQualifiedName~OpenApiDriftTest
/// </summary>
public class OpenApiDriftTest
{
    private const string UpdateEnvVar = "UPDATE_OPENAPI_BASELINE";

    [Fact]
    public async Task Live_openapi_document_matches_committed_baseline()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var liveJson = await response.Content.ReadAsStringAsync();

        var baselinePath = FindBaselinePath();

        if (Environment.GetEnvironmentVariable(UpdateEnvVar) == "1")
        {
            await File.WriteAllTextAsync(baselinePath, Normalize(liveJson));
            return;
        }

        File.Exists(baselinePath).Should().BeTrue(
            $"the OpenAPI drift baseline must exist at {baselinePath}. " +
            $"Run `{UpdateEnvVar}=1 dotnet test --filter FullyQualifiedName~OpenApiDriftTest` " +
            "to generate it.");

        var baselineJson = await File.ReadAllTextAsync(baselinePath);

        StructurallyEqual(liveJson, baselineJson).Should().BeTrue(
            $"the live OpenAPI document at /openapi/v1.json has drifted from the committed " +
            $"baseline at {baselinePath}. If the API change is deliberate, regenerate the " +
            $"baseline with `{UpdateEnvVar}=1 dotnet test --filter FullyQualifiedName~" +
            "OpenApiDriftTest` and commit the diff in the same PR.");
    }

    // Walks up from the test binary's directory to the repo root (identified by a .git folder)
    // and returns the baseline path. Keeps the baseline at repo root so both the compare read and
    // the UPDATE write hit the same source file — no copy-to-output indirection.
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

    // Emits the document with sorted object keys + indented formatting, so regenerating the
    // baseline is stable regardless of the OpenAPI generator's internal key ordering.
    private static string Normalize(string json)
    {
        using var doc = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            WriteSorted(writer, doc.RootElement);
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static void WriteSorted(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var prop in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(prop.Name);
                    WriteSorted(writer, prop.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteSorted(writer, item);
                }
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static bool StructurallyEqual(string a, string b)
    {
        using var da = JsonDocument.Parse(a);
        using var db = JsonDocument.Parse(b);
        return JsonElementsEqual(da.RootElement, db.RootElement);
    }

    private static bool JsonElementsEqual(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            return false;
        }

        return a.ValueKind switch
        {
            JsonValueKind.Object => ObjectsEqual(a, b),
            JsonValueKind.Array => ArraysEqual(a, b),
            JsonValueKind.String => a.GetString() == b.GetString(),
            JsonValueKind.Number => a.GetRawText() == b.GetRawText(),
            JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => true,
            _ => a.GetRawText() == b.GetRawText(),
        };
    }

    private static bool ObjectsEqual(JsonElement a, JsonElement b)
    {
        var aProps = a.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
        var bProps = b.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
        if (aProps.Count != bProps.Count)
        {
            return false;
        }
        for (var i = 0; i < aProps.Count; i++)
        {
            if (aProps[i].Name != bProps[i].Name) return false;
            if (!JsonElementsEqual(aProps[i].Value, bProps[i].Value)) return false;
        }
        return true;
    }

    // OpenAPI arrays (parameters, tags, security lists) are order-sensitive for readers, so we
    // compare positionally. If the generator turns out to be non-deterministic in some array's
    // ordering, revisit for that specific path rather than sorting everything.
    private static bool ArraysEqual(JsonElement a, JsonElement b)
    {
        if (a.GetArrayLength() != b.GetArrayLength()) return false;
        using var ea = a.EnumerateArray().GetEnumerator();
        using var eb = b.EnumerateArray().GetEnumerator();
        while (ea.MoveNext() && eb.MoveNext())
        {
            if (!JsonElementsEqual(ea.Current, eb.Current)) return false;
        }
        return true;
    }
}
