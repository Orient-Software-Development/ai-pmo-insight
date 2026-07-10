using AwesomeAssertions;
using AiPMOInsight.Domain.Findings;
using Xunit;

namespace AiPMOInsight.Application.Tests.Findings;

public class AnalysisRunTests
{
    [Fact]
    public void Start_creates_a_run_with_a_fresh_id_for_the_upload()
    {
        var uploadId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var run = AnalysisRun.Start(uploadId, now);

        run.RunId.Should().NotBe(Guid.Empty);
        run.UploadId.Should().Be(uploadId);
        run.StartedAt.Should().Be(now);
    }

    [Fact]
    public void Start_yields_a_new_run_id_each_time_so_re_analysis_appends()
    {
        var uploadId = Guid.NewGuid();

        var first = AnalysisRun.Start(uploadId, DateTimeOffset.UtcNow);
        var second = AnalysisRun.Start(uploadId, DateTimeOffset.UtcNow);

        // A second analysis of the same upload is a distinct run — its findings append, never
        // overwrite the first run's.
        second.RunId.Should().NotBe(first.RunId);
    }

    [Fact]
    public void Start_rejects_an_empty_upload_id()
    {
        var act = () => AnalysisRun.Start(Guid.Empty, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }
}
