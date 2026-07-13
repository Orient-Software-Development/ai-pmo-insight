using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using OpenAI;
using OpenAI.Chat;
using AwesomeAssertions;
using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.Analysis.Agents;
using AiPMOInsight.Infrastructure.Analysis.Llm;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// The real OpenAI Chat Completions adapter, exercised over a mock <see cref="HttpMessageHandler"/>
/// so no live key is needed in CI. Mirrors <see cref="AnthropicLlmClientTests"/>: structured-output
/// round-trip, model / budget mapping, typed-error mapping + secret-leak guard, and cancellation.
/// </summary>
public class OpenAiLlmClientTests
{
    private const string Secret = "sk-super-secret-value-do-not-log-123";

    private static LlmProviderOptions Options(string model = "gpt-4o-mini", int budget = 1024) =>
        new() { Provider = "openai", ModelId = model, ApiKey = Secret, PerAnalysisTokenBudget = budget };

    private static LlmRequest Request(string skill = "Challenge") =>
        new() { SkillName = skill, Prompt = "critique these findings", PromptVersion = "sha256:x" };

    /// <summary>Wraps a model text payload in a minimal Chat Completions response envelope.</summary>
    private static string Envelope(string modelText) => new JsonObject
    {
        ["id"] = "chatcmpl-1",
        ["object"] = "chat.completion",
        ["created"] = 0,
        ["model"] = "gpt-4o-mini",
        ["choices"] = new JsonArray(new JsonObject
        {
            ["index"] = 0,
            ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = modelText },
            ["finish_reason"] = "stop",
        }),
        ["usage"] = new JsonObject
        {
            ["prompt_tokens"] = 10,
            ["completion_tokens"] = 20,
            ["total_tokens"] = 30,
        },
    }.ToJsonString();

    private static ChatClient ChatClientOver(StubHandler handler) =>
        new("gpt-4o-mini", new ApiKeyCredential("test-key"),
            new OpenAIClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(handler)) });

    private static OpenAiLlmClient AdapterReturning(string modelText, LlmProviderOptions? options = null)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Envelope(modelText), Encoding.UTF8, "application/json"),
        });
        return new OpenAiLlmClient(options ?? Options(), ChatClientOver(handler));
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

    private static (OpenAiLlmClient adapter, Func<JsonObject> sentRequest) AdapterCapturing(LlmProviderOptions options)
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
        return (new OpenAiLlmClient(options, ChatClientOver(handler)), () => captured!);
    }

    [Fact]
    public void Empty_ModelId_falls_back_to_the_default_model()
    {
        // Model is bound to the ChatClient at construction, so resolution is unit-tested directly.
        OpenAiLlmClient.ResolveModel(Options(model: string.Empty)).Should().Be("gpt-4o-mini");
    }

    [Fact]
    public void Configured_ModelId_is_used_verbatim()
    {
        OpenAiLlmClient.ResolveModel(Options(model: "gpt-4o")).Should().Be("gpt-4o");
    }

    [Fact]
    public async Task PerAnalysisTokenBudget_maps_to_MaxCompletionTokens()
    {
        var (adapter, sent) = AdapterCapturing(Options(budget: 50_000));

        await adapter.CompleteAsync<ChallengeResult>(Request(), CancellationToken.None);

        sent()["max_completion_tokens"]!.GetValue<int>().Should().Be(50_000);
    }

    [Fact]
    public async Task Vendor_error_is_mapped_to_a_domain_failure_without_leaking_the_key()
    {
        // A 401 makes the SDK raise a System.ClientModel.ClientResultException — the adapter must
        // catch it and surface an LlmProviderException naming the skill, never the raw type or key.
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(
                "{\"error\":{\"message\":\"Incorrect API key provided\",\"type\":\"invalid_request_error\",\"code\":\"invalid_api_key\"}}",
                Encoding.UTF8, "application/json"),
        });
        // Key set on the client too, so the guard is meaningful if the SDK were to echo it anywhere.
        var client = new ChatClient("gpt-4o-mini", new ApiKeyCredential(Secret),
            new OpenAIClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(handler)) });
        var adapter = new OpenAiLlmClient(Options(), client);

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
