using AwesomeAssertions;
using AiPMOInsight.Application.Features.Analysis.Prompts;
using Xunit;

namespace AiPMOInsight.Application.Tests.Analysis;

public class PromptRegistryTests
{
    [Fact]
    public void Get_returns_content_and_a_content_hash_version()
    {
        var registry = new PromptRegistry(new Dictionary<string, string>
        {
            ["narrative"] = "Summarise the findings.",
        });

        var prompt = registry.Get("narrative");

        prompt.Name.Should().Be("narrative");
        prompt.Content.Should().Be("Summarise the findings.");
        prompt.Version.Should().StartWith("sha256:");
    }

    [Fact]
    public void Version_is_stable_for_identical_content_and_differs_for_changed_content()
    {
        var a = new PromptRegistry(new Dictionary<string, string> { ["p"] = "same text" });
        var b = new PromptRegistry(new Dictionary<string, string> { ["p"] = "same text" });
        var c = new PromptRegistry(new Dictionary<string, string> { ["p"] = "changed text" });

        a.Get("p").Version.Should().Be(b.Get("p").Version);
        a.Get("p").Version.Should().NotBe(c.Get("p").Version);
    }

    [Fact]
    public void Unknown_prompt_is_reported()
    {
        var registry = new PromptRegistry(new Dictionary<string, string> { ["known"] = "x" });

        registry.TryGet("missing", out _).Should().BeFalse();
        var act = () => registry.Get("missing");
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Embedded_registry_carries_the_four_llm_agent_prompts()
    {
        var registry = PromptRegistry.FromEmbeddedResources();

        // The four LLM-touching agents each have a prompt file shipped in the assembly.
        registry.TryGet("risk-and-issue", out _).Should().BeTrue();
        registry.TryGet("narrative", out _).Should().BeTrue();
        registry.TryGet("challenge", out _).Should().BeTrue();
        registry.TryGet("review", out _).Should().BeTrue();
    }
}
