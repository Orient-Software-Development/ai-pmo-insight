using AwesomeAssertions;
using AiPMOInsight.Api.Tests.Fixtures;
using AiPMOInsight.Infrastructure.Analysis.Parsing;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// Exercises the real Data Collector (#1) parsers against genuine OOXML / XML built by the fixture
/// builder. Pure deterministic parsing — no host, no LLM.
/// </summary>
public class DataCollectorParserTests
{
    private static readonly UploadParser Parser = new();

    [Fact]
    public void Excel_workbook_parses_into_all_tabular_categories()
    {
        var data = Parser.Parse("orbit.xlsx", OrbitFixtureBuilder.Workbook());

        data.Projects.Should().ContainSingle();
        data.Projects[0].Key.Should().Be("ALPHA");
        data.Projects[0].PercentComplete.Should().Be(45);

        data.Milestones.Should().HaveCount(2);
        data.BudgetLines.Should().ContainSingle();
        data.BudgetLines[0].Forecast.Should().Be(118000m);
        data.BudgetLines[0].Budget.Should().Be(100000m);

        data.Assignments.Should().ContainSingle();
        data.Assignments[0].AllocationPercent.Should().Be(120);

        data.RaidItems.Should().ContainSingle();
        data.RaidItems[0].Type.Should().Be(Application.Features.Analysis.Model.RaidType.Risk);
    }

    [Fact]
    public void Parsed_records_carry_a_source_locator_pointing_at_the_row()
    {
        var data = Parser.Parse("orbit.xlsx", OrbitFixtureBuilder.Workbook());

        // A budget finding must be able to cite the exact row it came from.
        data.BudgetLines[0].Source.Locator.Should().Contain("Budget");
        data.BudgetLines[0].Source.Locator.Should().Contain("2");
    }

    [Fact]
    public void Orbit_xml_parses_into_raid_items()
    {
        var data = Parser.Parse("orbit.xml", OrbitFixtureBuilder.OrbitXml());

        data.RaidItems.Should().HaveCount(2);
        data.RaidItems.Select(r => r.Type).Should().Contain(Application.Features.Analysis.Model.RaidType.Issue);
        data.RaidItems.Select(r => r.Type).Should().Contain(Application.Features.Analysis.Model.RaidType.Dependency);
    }

    [Fact]
    public void Docx_parses_into_minute_entries()
    {
        var data = Parser.Parse("minutes.docx", OrbitFixtureBuilder.MinutesDocx());

        data.Minutes.Should().NotBeEmpty();
        data.Minutes.Should().Contain(m => m.Text.Contains("slip"));
    }

    [Fact]
    public void Unknown_file_type_yields_empty_data_without_throwing()
    {
        var data = Parser.Parse("notes.txt", System.Text.Encoding.UTF8.GetBytes("hello"));

        data.Projects.Should().BeEmpty();
        data.Minutes.Should().BeEmpty();
    }
}
