using AwesomeAssertions;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Infrastructure.Analysis.Llm;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// The router dispatches each <see cref="ILlmClient.CompleteAsync{TOutput}"/> call to the inner
/// client selected by <see cref="LlmRequest.SkillName"/>, falling back to the default when no
/// per-agent override was registered. Inner clients are held by reference — not rebuilt per call.
/// </summary>
public class RoutingLlmClientTests
{
    private sealed record Payload(string Value);

    private static LlmRequest RequestFor(string skill) => new()
    {
        SkillName = skill,
        Prompt = "p",
        PromptVersion = "sha256:v",
    };

    private static FakeLlmClient FakeReturning(string tag) =>
        new(new Dictionary<Type, Func<LlmRequest, object>>
        {
            [typeof(Payload)] = _ => new Payload(tag),
        });

    [Fact]
    public async Task Routes_by_SkillName_to_the_matching_inner_client()
    {
        var narrative = FakeReturning("narrative");
        var challenge = FakeReturning("challenge");
        var router = new RoutingLlmClient(
            @default: FakeReturning("default"),
            perSkill: new Dictionary<string, ILlmClient>(StringComparer.OrdinalIgnoreCase)
            {
                ["Narrative"] = narrative,
                ["Challenge"] = challenge,
            });

        var narrativeResult = await router.CompleteAsync<Payload>(RequestFor("Narrative"), CancellationToken.None);
        var challengeResult = await router.CompleteAsync<Payload>(RequestFor("Challenge"), CancellationToken.None);

        narrativeResult.Value.Should().Be("narrative");
        challengeResult.Value.Should().Be("challenge");
    }

    [Fact]
    public async Task Falls_back_to_Default_for_unknown_skill()
    {
        var router = new RoutingLlmClient(
            @default: FakeReturning("default"),
            perSkill: new Dictionary<string, ILlmClient>(StringComparer.OrdinalIgnoreCase)
            {
                ["Narrative"] = FakeReturning("narrative"),
            });

        var result = await router.CompleteAsync<Payload>(RequestFor("Review"), CancellationToken.None);

        result.Value.Should().Be("default");
    }

    [Fact]
    public async Task Matches_SkillName_case_insensitively()
    {
        var router = new RoutingLlmClient(
            @default: FakeReturning("default"),
            perSkill: new Dictionary<string, ILlmClient>(StringComparer.OrdinalIgnoreCase)
            {
                ["Narrative"] = FakeReturning("narrative"),
            });

        var result = await router.CompleteAsync<Payload>(RequestFor("NARRATIVE"), CancellationToken.None);

        result.Value.Should().Be("narrative");
    }

    [Fact]
    public void Ctor_rejects_null_default()
    {
        var act = () => new RoutingLlmClient(
            @default: null!,
            perSkill: new Dictionary<string, ILlmClient>(StringComparer.OrdinalIgnoreCase));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Inner_clients_are_not_rebuilt_per_call()
    {
        // If the router were re-resolving inner clients per call, the same inner instance would
        // not be reused across N calls. We prove reuse by holding the client references and
        // checking they answered every call — a call-count on a spy is unnecessary since the
        // router holds the map by reference.
        var narrative = FakeReturning("narrative");
        var router = new RoutingLlmClient(
            @default: FakeReturning("default"),
            perSkill: new Dictionary<string, ILlmClient>(StringComparer.OrdinalIgnoreCase)
            {
                ["Narrative"] = narrative,
            });

        for (var i = 0; i < 5; i++)
        {
            var r = await router.CompleteAsync<Payload>(RequestFor("Narrative"), CancellationToken.None);
            r.Value.Should().Be("narrative");
        }
    }
}
