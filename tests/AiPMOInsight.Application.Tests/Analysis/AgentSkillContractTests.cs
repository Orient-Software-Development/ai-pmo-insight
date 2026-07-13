using AwesomeAssertions;
using AiPMOInsight.Application.Features.Analysis;
using Xunit;

namespace AiPMOInsight.Application.Tests.Analysis;

public class AgentSkillContractTests
{
    // A trivial skill proving the typed input/output contract the orchestrator relies on.
    private sealed class DoublingSkill : IAgentSkill<int, int>
    {
        public string Name => "Doubling";

        public Task<int> ExecuteAsync(int input, CancellationToken cancellationToken) =>
            Task.FromResult(input * 2);
    }

    [Fact]
    public async Task Skill_exposes_a_name_and_maps_input_to_typed_output()
    {
        IAgentSkill<int, int> skill = new DoublingSkill();

        skill.Name.Should().Be("Doubling");
        (await skill.ExecuteAsync(21, CancellationToken.None)).Should().Be(42);
    }
}
