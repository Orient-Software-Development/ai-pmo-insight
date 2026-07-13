using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Anthropic;
using AwesomeAssertions;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.Analysis.Agents;
using AiPMOInsight.Infrastructure.Analysis.Llm;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// The real Anthropic Messages API adapter (issue #27), exercised over a mock <see cref="HttpMessageHandler"/>
/// so no live key is needed in CI. Covers structured-output round-trip, model/budget mapping, typed-error
/// mapping + secret-leak guard, and cancellation.
/// </summary>
public class AnthropicLlmClientTests
{
    private const string Secret = "sk-super-secret-value-do-not-log-123";

    private static LlmProviderOptions Options(string model = "claude-opus-4-8", int budget = 1024) =>
        new() { Provider = "anthropic", ModelId = model, ApiKey = Secret, PerAnalysisTokenBudget = budget };

    private static LlmRequest Request(string skill = "Challenge") =>
        new() { SkillName = skill, Prompt = "critique these findings", PromptVersion = "sha256:x" };

    /// <summary>Wraps a model text payload in a minimal Messages API response envelope.</summary>
    private static string Envelope(string modelText) => new JsonObject
    {
        ["id"] = "msg_1",
        ["type"] = "message",
        ["role"] = "assistant",
        ["model"] = "claude-opus-4-8",
        ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = modelText }),
        ["stop_reason"] = "end_turn",
        ["usage"] = new JsonObject { ["input_tokens"] = 10, ["output_tokens"] = 20 },
    }.ToJsonString();

    private static AnthropicLlmClient AdapterReturning(string modelText, LlmProviderOptions? options = null)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Envelope(modelText), Encoding.UTF8, "application/json"),
        });
        var client = new AnthropicClient { ApiKey = "test-key", HttpClient = new HttpClient(handler) };
        return new AnthropicLlmClient(options ?? Options(), client);
    }

    [Fact]
    public async Task CompleteAsync_deserialises_structured_output_into_a_fully_populated_result()
    {
        var modelText = new JsonObject
        {
            ["Critiques"] = new JsonArray(new JsonObject
            {
                ["Target"] = "finding-1",
                ["Concern"] = "weak evidence",
                ["Severity"] = "high",
                ["Suggestion"] = "cite the source",
            }),
        }.ToJsonString();

        var adapter = AdapterReturning(modelText);

        var result = await adapter.CompleteAsync<ChallengeResult>(Request(), CancellationToken.None);

        result.Critiques.Should().ContainSingle();
        var critique = result.Critiques[0];
        critique.Target.Should().Be("finding-1");
        critique.Concern.Should().Be("weak evidence");
        critique.Severity.Should().Be("high");
        critique.Suggestion.Should().Be("cite the source");
    }

    private static (AnthropicLlmClient adapter, Func<JsonObject> sentRequest) AdapterCapturing(LlmProviderOptions options)
    {
        // A well-formed ChallengeResult so CompleteAsync completes; we only care about the request here.
        var modelText = new JsonObject
        {
            ["Critiques"] = new JsonArray(new JsonObject
            {
                ["Target"] = "t",
                ["Concern"] = "c",
                ["Severity"] = "s",
                ["Suggestion"] = "sg",
            }),
        }.ToJsonString();

        JsonObject? captured = null;
        var handler = new StubHandler(req =>
        {
            var body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            captured = JsonNode.Parse(body)!.AsObject();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Envelope(modelText), Encoding.UTF8, "application/json"),
            };
        });
        var client = new AnthropicClient { ApiKey = "test-key", HttpClient = new HttpClient(handler) };
        return (new AnthropicLlmClient(options, client), () => captured!);
    }

    [Fact]
    public async Task Empty_ModelId_falls_back_to_the_default_model()
    {
        var (adapter, sent) = AdapterCapturing(Options(model: string.Empty));

        await adapter.CompleteAsync<ChallengeResult>(Request(), CancellationToken.None);

        sent()["model"]!.GetValue<string>().Should().Be("claude-opus-4-8");
    }

    [Fact]
    public async Task Configured_ModelId_is_used_verbatim()
    {
        var (adapter, sent) = AdapterCapturing(Options(model: "claude-sonnet-5"));

        await adapter.CompleteAsync<ChallengeResult>(Request(), CancellationToken.None);

        sent()["model"]!.GetValue<string>().Should().Be("claude-sonnet-5");
    }

    [Fact]
    public async Task PerAnalysisTokenBudget_maps_to_MaxTokens()
    {
        var (adapter, sent) = AdapterCapturing(Options(budget: 50_000));

        await adapter.CompleteAsync<ChallengeResult>(Request(), CancellationToken.None);

        sent()["max_tokens"]!.GetValue<int>().Should().Be(50_000);
    }

    [Fact]
    public async Task Vendor_error_is_mapped_to_a_domain_failure_without_leaking_the_key()
    {
        // A 401 makes the SDK raise a typed Anthropic.Exceptions.* — the adapter must catch it and
        // surface an LlmProviderException naming the skill, never the raw vendor type or the key.
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(
                "{\"type\":\"error\",\"error\":{\"type\":\"authentication_error\",\"message\":\"invalid x-api-key\"}}",
                Encoding.UTF8, "application/json"),
        });
        // Key set on the client too, so the guard is meaningful if the SDK were to echo it anywhere.
        var client = new AnthropicClient { ApiKey = Secret, HttpClient = new HttpClient(handler) };
        var adapter = new AnthropicLlmClient(Options(), client);

        var act = async () => await adapter.CompleteAsync<ChallengeResult>(Request(), CancellationToken.None);

        var thrown = (await act.Should().ThrowAsync<LlmProviderException>()).Which;
        thrown.Message.Should().Contain("Challenge");
        thrown.ToString().Should().NotContain(Secret);
    }

    [Fact]
    public async Task Cancelled_token_aborts_the_call()
    {
        var adapter = AdapterReturning("{\"Critiques\":[]}");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await adapter.CompleteAsync<ChallengeResult>(Request(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
