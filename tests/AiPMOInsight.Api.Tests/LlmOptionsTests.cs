using AwesomeAssertions;
using AiPMOInsight.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// The <c>Llm</c> config section is inert this slice (the fake ignores it) but must be wired so the
/// real adapter next change is a config-only swap. This confirms the host binds it.
/// </summary>
public class LlmOptionsTests
{
    [Fact]
    public void Host_binds_the_Llm_options_section()
    {
        using var factory = new TestWebAppFactory();
        _ = factory.CreateClient(); // force the host to build

        var options = factory.Services.GetRequiredService<IOptions<LlmOptions>>().Value;

        // Provider is the swap point — defaults to the fake this slice.
        options.Provider.Should().Be("fake");
        options.PerAnalysisTokenBudget.Should().BeGreaterThan(0);
    }
}
