using AiPMOInsight.Api.Tests.Fixtures;
using Xunit;

namespace AiPMOInsight.Api.Tests;

// TEMPORARY — writes the dummy Orbit workbook to disk for manual end-to-end verification (task 8.2).
// Runs only when DUMP_FIXTURE_PATH is set. Delete this file after verification.
public class _TempFixtureDump
{
    [Fact]
    public void Dump_workbook()
    {
        var path = Environment.GetEnvironmentVariable("DUMP_FIXTURE_PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return; // no-op unless explicitly requested
        }

        File.WriteAllBytes(path, OrbitFixtureBuilder.Workbook());
    }
}
