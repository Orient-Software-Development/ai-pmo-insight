using AwesomeAssertions;
using AiPMOInsight.Application.Features.HealthScoring;
using AiPMOInsight.Domain.Findings;
using Xunit;

namespace AiPMOInsight.Application.Tests.HealthScoring;

public class HealthScoringOptionsTests
{
    /// <summary>A valid EXAMPLE-shaped options object; tests mutate one facet to force a failure.</summary>
    private static HealthScoringOptions Valid() => new()
    {
        WeightTotal = 100,
        Weights = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Schedule"] = 20,
            ["Budget"] = 30,
            ["Risk"] = 30,
            ["Resource"] = 15,
            ["DataQuality"] = 5,
        },
        SeverityScores = new(StringComparer.OrdinalIgnoreCase) { ["Green"] = 100, ["Amber"] = 70, ["Red"] = 30 },
        Thresholds = new RagThresholds { Green = 80, Amber = 60 },
        ConfidenceScores = new(StringComparer.OrdinalIgnoreCase) { ["Low"] = 30, ["Medium"] = 70, ["High"] = 100 },
        ConfidenceFloor = 50,
        Overrides =
        [
            new OverrideRuleOptions { Id = "budget-critical", Area = "Budget", WhenSeverityAtLeast = "Red", Floor = "Red" },
        ],
    };

    [Fact]
    public void Valid_options_pass_validation()
    {
        var act = () => Valid().Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Typed_accessors_resolve_by_enum()
    {
        var options = Valid();

        options.WeightFor(HealthArea.Schedule).Should().Be(20);
        options.ScoreFor(Severity.Amber).Should().Be(70);
        options.ScoreFor(Confidence.High).Should().Be(100);
    }

    [Fact]
    public void Weights_that_do_not_sum_to_the_total_fail_naming_the_key()
    {
        var options = Valid();
        options.Weights["Schedule"] = 25; // now sums to 105

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Weights*");
    }

    [Fact]
    public void Unordered_thresholds_fail_naming_the_key()
    {
        var options = Valid();
        options.Thresholds = new RagThresholds { Green = 60, Amber = 80 }; // Green below Amber

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Thresholds*");
    }

    [Fact]
    public void Unknown_weight_area_fails_naming_the_key()
    {
        var options = Valid();
        options.Weights["Nonsense"] = 0;

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Weights*Nonsense*");
    }

    [Fact]
    public void Missing_severity_mapping_fails()
    {
        var options = Valid();
        options.SeverityScores.Remove("Red");

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SeverityScores*Red*");
    }

    [Fact]
    public void Override_with_unparseable_enum_fails_naming_the_rule()
    {
        var options = Valid();
        options.Overrides.Add(new OverrideRuleOptions { Id = "bad", Area = "Nope", WhenSeverityAtLeast = "Red", Floor = "Amber" });

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Overrides*bad*");
    }

    [Fact]
    public void Decision_is_a_recognised_weighted_area()
    {
        var options = Valid();
        // Rebalance to fund a Decision weight while keeping the configured weights summing to WeightTotal.
        options.Weights["Budget"] = 25;
        options.Weights["Risk"] = 25;
        options.Weights["Decision"] = 10;

        var act = () => options.Validate();

        act.Should().NotThrow();
        options.WeightFor(HealthArea.Decision).Should().Be(10);
    }
}
