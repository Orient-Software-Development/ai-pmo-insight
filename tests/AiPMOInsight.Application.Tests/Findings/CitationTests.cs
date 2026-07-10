using AwesomeAssertions;
using AiPMOInsight.Domain.Findings;
using Xunit;

namespace AiPMOInsight.Application.Tests.Findings;

public class CitationTests
{
    [Fact]
    public void Create_defaults_optional_evidence_to_null()
    {
        var citation = Citation.Create(Guid.NewGuid(), "Budget!row3");

        citation.StructuredExcerpt.Should().BeNull();
        citation.TextSnippet.Should().BeNull();
    }

    [Fact]
    public void Create_carries_structured_excerpt_and_text_snippet()
    {
        var uploadId = Guid.NewGuid();

        var citation = Citation.Create(
            uploadId,
            locator: "Budget!B4",
            structuredExcerpt: "sheet=Budget;row=4;col=B",
            textSnippet: "Forecast exceeds approved budget by 18%.");

        citation.UploadId.Should().Be(uploadId);
        citation.Locator.Should().Be("Budget!B4");
        citation.StructuredExcerpt.Should().Be("sheet=Budget;row=4;col=B");
        citation.TextSnippet.Should().Be("Forecast exceeds approved budget by 18%.");
    }

    [Fact]
    public void Create_normalizes_blank_optional_evidence_to_null()
    {
        var citation = Citation.Create(Guid.NewGuid(), "loc", structuredExcerpt: "   ", textSnippet: "");

        citation.StructuredExcerpt.Should().BeNull();
        citation.TextSnippet.Should().BeNull();
    }

    [Fact]
    public void Create_still_guards_the_mandatory_fields()
    {
        var emptyUpload = () => Citation.Create(Guid.Empty, "loc");
        var blankLocator = () => Citation.Create(Guid.NewGuid(), "  ");

        emptyUpload.Should().Throw<ArgumentException>();
        blankLocator.Should().Throw<ArgumentException>();
    }
}
