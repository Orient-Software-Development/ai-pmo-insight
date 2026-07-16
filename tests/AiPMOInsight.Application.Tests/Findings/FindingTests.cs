using AwesomeAssertions;
using AiPMOInsight.Domain.Findings;
using Xunit;

namespace AiPMOInsight.Application.Tests.Findings;

public class FindingTests
{
    private static Finding Create(
        string projectKey = "PRJ-1",
        string summary = "summary",
        Citation? citation = null,
        Guid? runId = null,
        string producingAgent = "test",
        FindingKind kind = FindingKind.Analysis,
        Confidence confidence = Confidence.Medium,
        string? promptVersion = null,
        HealthArea? area = HealthArea.Schedule,
        Severity? severity = Severity.Amber,
        decimal? metricValue = null,
        string? metricUnit = null,
        IReadOnlyDictionary<string, string>? metricDetail = null) =>
        Finding.Create(
            projectKey,
            summary,
            citation ?? Citation.Create(Guid.NewGuid(), "file.csv#row1"),
            DateTimeOffset.UtcNow,
            runId ?? Guid.NewGuid(),
            producingAgent,
            kind,
            confidence,
            promptVersion,
            area,
            severity,
            metricValue,
            metricUnit,
            metricDetail);

    [Fact]
    public void Create_requires_a_citation()
    {
        // Call the factory directly so the null citation is not coalesced away by the helper.
        var act = () => Finding.Create(
            "PRJ-1", "summary", citation: null!, DateTimeOffset.UtcNow,
            runId: Guid.NewGuid(), producingAgent: "test", kind: FindingKind.Analysis, confidence: Confidence.Medium);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_rejects_blank_project_key()
    {
        var act = () => Create(projectKey: "  ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_blank_producing_agent()
    {
        var act = () => Create(producingAgent: "  ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_empty_run_id()
    {
        var act = () => Create(runId: Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_carries_provenance_through()
    {
        var runId = Guid.NewGuid();

        var finding = Create(
            runId: runId,
            producingAgent: "Financial",
            kind: FindingKind.Narrative,
            confidence: Confidence.High,
            promptVersion: "sha256:abc123");

        finding.RunId.Should().Be(runId);
        finding.ProducingAgent.Should().Be("Financial");
        finding.Kind.Should().Be(FindingKind.Narrative);
        finding.Confidence.Should().Be(Confidence.High);
        finding.PromptVersion.Should().Be("sha256:abc123");
    }

    [Fact]
    public void Create_defaults_prompt_version_to_null_for_deterministic_findings()
    {
        var finding = Create();

        finding.PromptVersion.Should().BeNull();
    }

    [Fact]
    public void Create_carries_the_citation_through()
    {
        var uploadId = Guid.NewGuid();
        var citation = Citation.Create(uploadId, "file.csv#row1");

        var finding = Create(citation: citation);

        finding.Citation.UploadId.Should().Be(uploadId);
        finding.Citation.Locator.Should().Be("file.csv#row1");
    }

    [Fact]
    public void Analysis_finding_carries_area_and_severity()
    {
        var finding = Create(kind: FindingKind.Analysis, area: HealthArea.Budget, severity: Severity.Red);

        finding.Area.Should().Be(HealthArea.Budget);
        finding.Severity.Should().Be(Severity.Red);
    }

    [Theory]
    [InlineData(FindingKind.Narrative)]
    [InlineData(FindingKind.Challenge)]
    [InlineData(FindingKind.Review)]
    public void Non_analysis_findings_have_no_area_or_severity(FindingKind kind)
    {
        // Even if an area/severity is passed, a non-analysis finding leaves them null (the fields
        // only describe deterministic Analysis findings).
        var finding = Create(kind: kind, area: HealthArea.Schedule, severity: Severity.Amber);

        finding.Area.Should().BeNull();
        finding.Severity.Should().BeNull();
    }

    [Fact]
    public void Create_throws_when_analysis_finding_has_no_area()
    {
        var act = () => Create(kind: FindingKind.Analysis, area: null, severity: Severity.Amber);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_throws_when_analysis_finding_has_no_severity()
    {
        var act = () => Create(kind: FindingKind.Analysis, area: HealthArea.Schedule, severity: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_accepts_an_optional_numeric_metric()
    {
        var finding = Create(metricValue: 80000m, metricUnit: "EUR");

        finding.MetricValue.Should().Be(80000m);
        finding.MetricUnit.Should().Be("EUR");
    }

    [Fact]
    public void Create_accepts_an_optional_detail_map()
    {
        var detail = new Dictionary<string, string> { ["owner"] = "PMO Director", ["deadline"] = "next week" };

        var finding = Create(metricDetail: detail);

        finding.MetricDetail.Should().NotBeNull();
        finding.MetricDetail!["owner"].Should().Be("PMO Director");
        finding.MetricDetail!["deadline"].Should().Be("next week");
    }

    [Fact]
    public void Create_leaves_the_metric_null_when_not_supplied()
    {
        var finding = Create();

        finding.MetricValue.Should().BeNull();
        finding.MetricUnit.Should().BeNull();
        finding.MetricDetail.Should().BeNull();
    }

    [Fact]
    public void Analysis_invariants_still_hold_with_a_metric_present()
    {
        // A metric is orthogonal to the existing invariants: a Kind==Analysis finding with no area still
        // throws even when a metric is supplied.
        var act = () => Create(kind: FindingKind.Analysis, area: null, severity: Severity.Amber, metricValue: 5m);

        act.Should().Throw<ArgumentException>();
    }
}
