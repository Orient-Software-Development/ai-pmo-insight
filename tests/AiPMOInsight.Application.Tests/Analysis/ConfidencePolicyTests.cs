using AwesomeAssertions;
using AiPMOInsight.Application.Features.Analysis;
using AiPMOInsight.Domain.Findings;
using Xunit;

namespace AiPMOInsight.Application.Tests.Analysis;

public class ConfidencePolicyTests
{
    [Fact]
    public void Clean_recent_consistent_data_is_high_confidence()
    {
        var signal = new DataQualitySignal { MissingFieldCount = 0, LastUpdateAgeDays = 2, SourceConsistent = true };

        ConfidencePolicy.FromSignals(signal).Should().Be(Confidence.High);
    }

    [Theory]
    [InlineData(1, Confidence.Medium)]
    [InlineData(3, Confidence.Low)]
    public void Missing_fields_knock_confidence_down(int missing, Confidence expected)
    {
        var signal = new DataQualitySignal { MissingFieldCount = missing, LastUpdateAgeDays = 0, SourceConsistent = true };

        ConfidencePolicy.FromSignals(signal).Should().Be(expected);
    }

    [Theory]
    [InlineData(45, Confidence.Medium)]
    [InlineData(120, Confidence.Low)]
    public void Stale_updates_knock_confidence_down(double ageDays, Confidence expected)
    {
        var signal = new DataQualitySignal { MissingFieldCount = 0, LastUpdateAgeDays = ageDays, SourceConsistent = true };

        ConfidencePolicy.FromSignals(signal).Should().Be(expected);
    }

    [Fact]
    public void Inconsistent_sources_knock_confidence_down()
    {
        var signal = new DataQualitySignal { MissingFieldCount = 0, LastUpdateAgeDays = 0, SourceConsistent = false };

        ConfidencePolicy.FromSignals(signal).Should().Be(Confidence.Medium);
    }

    [Fact]
    public void Cap_limits_self_reported_confidence_to_the_data_quality_confidence()
    {
        // An LLM agent cannot claim more confidence than the data it rests on supports.
        ConfidencePolicy.Cap(Confidence.High, Confidence.Medium).Should().Be(Confidence.Medium);
        ConfidencePolicy.Cap(Confidence.Low, Confidence.High).Should().Be(Confidence.Low);
        ConfidencePolicy.Cap(Confidence.High, Confidence.High).Should().Be(Confidence.High);
    }
}
