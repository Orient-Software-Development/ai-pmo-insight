using AwesomeAssertions;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Infrastructure.Analysis.Llm;
using Xunit;

namespace AiPMOInsight.Api.Tests;

public class FakeLlmClientTests
{
    private sealed record Foo(string Bar);

    private static LlmRequest Request => new() { SkillName = "X", Prompt = "p", PromptVersion = "sha256:v" };

    [Fact]
    public async Task Returns_the_registered_fixture_for_the_requested_type()
    {
        var fake = new FakeLlmClient(new Dictionary<Type, Func<LlmRequest, object>>
        {
            [typeof(Foo)] = _ => new Foo("baz"),
        });

        var result = await fake.CompleteAsync<Foo>(Request, CancellationToken.None);

        result.Bar.Should().Be("baz");
    }

    [Fact]
    public async Task Throws_for_a_type_with_no_fixture()
    {
        var fake = new FakeLlmClient(new Dictionary<Type, Func<LlmRequest, object>>());

        var act = async () => await fake.CompleteAsync<Foo>(Request, CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>();
    }
}
