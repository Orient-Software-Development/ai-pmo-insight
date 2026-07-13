using AwesomeAssertions;
using AiPMOInsight.Application.Features.Analysis.Model;
using Xunit;

namespace AiPMOInsight.Application.Tests.Analysis;

public class TypedRecordTests
{
    [Fact]
    public void SourceRef_becomes_a_citation_for_the_analyzed_upload()
    {
        var uploadId = Guid.NewGuid();
        var source = new SourceRef("Budget!B4", StructuredExcerpt: "sheet=Budget;row=4;col=B", TextSnippet: "Forecast 118,000");

        var citation = source.ToCitation(uploadId);

        citation.UploadId.Should().Be(uploadId);
        citation.Locator.Should().Be("Budget!B4");
        citation.StructuredExcerpt.Should().Be("sheet=Budget;row=4;col=B");
        citation.TextSnippet.Should().Be("Forecast 118,000");
    }

    [Fact]
    public void SourceRef_locator_only_produces_a_minimal_citation()
    {
        var citation = new SourceRef("Projects!row2").ToCitation(Guid.NewGuid());

        citation.Locator.Should().Be("Projects!row2");
        citation.StructuredExcerpt.Should().BeNull();
        citation.TextSnippet.Should().BeNull();
    }

    [Fact]
    public void Collected_data_groups_the_typed_records_and_each_carries_a_source()
    {
        var source = new SourceRef("x");
        var data = new CollectedData
        {
            Projects = [new ProjectRecord { Key = "ALPHA", Name = "Alpha", Source = source }],
            Milestones = [new MilestoneRecord { ProjectKey = "ALPHA", Name = "Design", Source = source }],
            BudgetLines = [new BudgetLineRecord { ProjectKey = "ALPHA", Category = "Dev", Budget = 100m, Forecast = 118m, Actual = 60m, Source = source }],
            Assignments = [new AssignmentRecord { ProjectKey = "ALPHA", Person = "Sam", Role = "Dev", AllocationPercent = 120, Source = source }],
            Minutes = [new MinuteEntryRecord { ProjectKey = "ALPHA", Date = DateTimeOffset.UtcNow, Text = "Vendor slip discussed", Source = source }],
            RaidItems = [new RaidItemRecord { ProjectKey = "ALPHA", Type = RaidType.Risk, Description = "Vendor risk", Source = source }],
        };

        data.Projects.Should().ContainSingle().Which.Source.Should().Be(source);
        data.BudgetLines[0].Forecast.Should().Be(118m);
        data.RaidItems[0].Type.Should().Be(RaidType.Risk);
    }
}
