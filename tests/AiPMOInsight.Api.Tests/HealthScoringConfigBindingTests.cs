using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AiPMOInsight.Application.Features.HealthScoring;
using AiPMOInsight.Domain.Findings;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// Confirms the <c>HealthScoring</c> config section binds to <see cref="HealthScoringOptions"/> across
/// every facet (weights, severity/confidence mappings, thresholds, override rules, confidence floor)
/// and that the shipped appsettings default is a valid, labelled placeholder.
/// </summary>
public class HealthScoringConfigBindingTests
{
    private static readonly Dictionary<string, string?> FullSection = new()
    {
        ["HealthScoring:IsPlaceholder"] = "true",
        ["HealthScoring:WeightTotal"] = "100",
        ["HealthScoring:Weights:Schedule"] = "20",
        ["HealthScoring:Weights:Budget"] = "30",
        ["HealthScoring:Weights:Risk"] = "30",
        ["HealthScoring:Weights:Resource"] = "15",
        ["HealthScoring:Weights:DataQuality"] = "5",
        ["HealthScoring:SeverityScores:Green"] = "100",
        ["HealthScoring:SeverityScores:Amber"] = "70",
        ["HealthScoring:SeverityScores:Red"] = "30",
        ["HealthScoring:Thresholds:Green"] = "80",
        ["HealthScoring:Thresholds:Amber"] = "60",
        ["HealthScoring:ConfidenceScores:Low"] = "30",
        ["HealthScoring:ConfidenceScores:Medium"] = "70",
        ["HealthScoring:ConfidenceScores:High"] = "100",
        ["HealthScoring:ConfidenceFloor"] = "50",
        ["HealthScoring:Overrides:0:Id"] = "budget-critical",
        ["HealthScoring:Overrides:0:Area"] = "Budget",
        ["HealthScoring:Overrides:0:WhenSeverityAtLeast"] = "Red",
        ["HealthScoring:Overrides:0:Floor"] = "Red",
    };

    private static HealthScoringOptions Bind(Dictionary<string, string?> settings)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var options = new HealthScoringOptions();
        config.GetSection(HealthScoringOptions.SectionName).Bind(options);
        return options;
    }

    [Fact]
    public void Binds_every_facet_of_the_section()
    {
        var options = Bind(FullSection);

        options.IsPlaceholder.Should().BeTrue();
        options.WeightTotal.Should().Be(100);
        options.WeightFor(HealthArea.Schedule).Should().Be(20);
        options.WeightFor(HealthArea.DataQuality).Should().Be(5);
        options.ScoreFor(Severity.Green).Should().Be(100);
        options.ScoreFor(Severity.Red).Should().Be(30);
        options.Thresholds.Green.Should().Be(80);
        options.Thresholds.Amber.Should().Be(60);
        options.ScoreFor(Confidence.Low).Should().Be(30);
        options.ConfidenceFloor.Should().Be(50);

        options.Overrides.Should().ContainSingle();
        var rule = options.Overrides[0];
        rule.Id.Should().Be("budget-critical");
        rule.AreaEnum.Should().Be(HealthArea.Budget);
        rule.WhenSeverityAtLeastEnum.Should().Be(Severity.Red);
        rule.FloorEnum.Should().Be(Severity.Red);
    }

    [Fact]
    public void Bound_section_passes_validation()
    {
        var act = () => Bind(FullSection).Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Shipped_appsettings_default_boots_and_registers_a_placeholder()
    {
        // Booting the real app runs AddInfrastructure, which binds + Validate()s the shipped
        // HealthScoring section — so a successful boot proves the default config is valid. The
        // registered instance is the EXAMPLE placeholder until a deployment overrides it.
        using var factory = new TestWebAppFactory();
        using var scope = factory.Services.CreateScope();

        var options = scope.ServiceProvider.GetRequiredService<HealthScoringOptions>();

        options.IsPlaceholder.Should().BeTrue("the shipped values are the PRD EXAMPLE placeholders");
        options.WeightFor(HealthArea.Schedule).Should().BeGreaterThan(0);
    }
}
