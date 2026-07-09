using AwesomeAssertions;
using AiPMOInsight.Domain.Findings;
using Xunit;

namespace AiPMOInsight.Application.Tests.Findings;

public class FindingTests
{
    [Fact]
    public void Create_requires_a_citation()
    {
        var act = () => Finding.Create("DUMMY-001", "summary", citation: null!, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_rejects_blank_project_key()
    {
        var citation = Citation.Create(Guid.NewGuid(), "file.csv#row1");

        var act = () => Finding.Create("  ", "summary", citation, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Citation_rejects_empty_upload_id_and_locator()
    {
        var emptyUpload = () => Citation.Create(Guid.Empty, "loc");
        var emptyLocator = () => Citation.Create(Guid.NewGuid(), "  ");

        emptyUpload.Should().Throw<ArgumentException>();
        emptyLocator.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_carries_the_citation_through()
    {
        var uploadId = Guid.NewGuid();
        var citation = Citation.Create(uploadId, "file.csv#row1");

        var finding = Finding.Create("DUMMY-001", "summary", citation, DateTimeOffset.UtcNow);

        finding.Citation.UploadId.Should().Be(uploadId);
        finding.Citation.Locator.Should().Be("file.csv#row1");
    }
}
