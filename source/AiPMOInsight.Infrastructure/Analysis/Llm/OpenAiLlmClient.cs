using System.ClientModel;
using System.Text.Json;
using OpenAI.Chat;
using AiPMOInsight.Application.Abstractions;

namespace AiPMOInsight.Infrastructure.Analysis.Llm;

/// <summary>
/// OpenAI Chat Completions adapter. Recognised by the factory as <c>Provider = "openai"</c>.
/// Requests <b>structured JSON output</b> constrained to a schema derived from <c>TOutput</c>
/// (<see cref="JsonSchemaGenerator"/>) and deserialises the returned text into <c>TOutput</c> —
/// never free-text parsing (mirrors <see cref="AnthropicLlmClient"/>). Model / key / token budget
/// come from the resolved <see cref="LlmProviderOptions"/>. The configured
/// <see cref="LlmProviderOptions.ApiKey"/> is never surfaced in an exception, log line, or telemetry
/// (R3 secret-leak guard).
/// </summary>
public sealed class OpenAiLlmClient : ILlmClient
{
    /// <summary>Model used when the resolved options leave <see cref="LlmProviderOptions.ModelId"/> empty.</summary>
    private const string DefaultModel = "gpt-4o-mini";

    /// <summary>
    /// Placeholder used when no key is configured so eager DI construction succeeds (a missing key is
    /// a request-time 401, not a startup failure — same contract as the Anthropic adapter). Never the
    /// real key, so it is safe if it ever surfaced.
    /// </summary>
    private const string MissingKeyPlaceholder = "missing-api-key";

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly LlmProviderOptions _options;
    private readonly ChatClient _client;

    /// <summary>Production constructor — builds a <see cref="ChatClient"/> from the options' model + key.</summary>
    public OpenAiLlmClient(LlmProviderOptions options)
        : this(options, BuildClient(options))
    {
    }

    /// <summary>Test seam: inject a pre-built client (e.g. one backed by a mock HTTP transport).</summary>
    internal OpenAiLlmClient(LlmProviderOptions options, ChatClient client)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>The model the adapter will use: the configured id, or <see cref="DefaultModel"/> when empty.</summary>
    internal static string ResolveModel(LlmProviderOptions options) =>
        string.IsNullOrWhiteSpace(options.ModelId) ? DefaultModel : options.ModelId;

    private static ChatClient BuildClient(LlmProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var key = string.IsNullOrEmpty(options.ApiKey) ? MissingKeyPlaceholder : options.ApiKey;
        return new ChatClient(ResolveModel(options), new ApiKeyCredential(key));
    }

    public async Task<TOutput> CompleteAsync<TOutput>(LlmRequest request, CancellationToken cancellationToken)
        where TOutput : notnull
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var schema = BinaryData.FromString(JsonSchemaGenerator.For<TOutput>().ToJsonString());
        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = _options.PerAnalysisTokenBudget,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: typeof(TOutput).Name,
                jsonSchema: schema,
                jsonSchemaIsStrict: true),
        };

        // OpenAI performs prefix caching automatically on identical initial content across calls;
        // splitting the stable instructional prompt into a system message keeps the front of the
        // input identical between per-project calls in one upload so caching kicks in.
        var messages = string.IsNullOrWhiteSpace(request.SystemPrompt)
            ? (IReadOnlyList<ChatMessage>)[new UserChatMessage(request.Prompt)]
            : [new SystemChatMessage(request.SystemPrompt), new UserChatMessage(request.Prompt)];

        ChatCompletion completion;
        try
        {
            var result = await _client.CompleteChatAsync(messages, options, cancellationToken);
            completion = result.Value;
        }
        catch (OperationCanceledException)
        {
            throw; // honour cancellation — never wrap it as a provider failure
        }
        catch (ClientResultException ex)
        {
            // R3 secret-leak guard: name the provider + skill, never the ApiKey. The SDK exception
            // (carrying only the server error body + request id) is preserved as the inner exception.
            throw new LlmProviderException(
                $"OpenAI call failed for skill '{request.SkillName}' (provider 'openai').", ex);
        }

        var text = completion.Content
            .Where(part => part.Kind == ChatMessageContentPartKind.Text)
            .Select(part => part.Text)
            .FirstOrDefault(static s => !string.IsNullOrWhiteSpace(s));

        if (text is null)
        {
            throw new InvalidOperationException(
                $"OpenAI returned no text content for skill '{request.SkillName}'.");
        }

        return JsonSerializer.Deserialize<TOutput>(text, DeserializeOptions)
            ?? throw new InvalidOperationException(
                $"OpenAI response for skill '{request.SkillName}' did not deserialise into " +
                $"{typeof(TOutput).Name}.");
    }
}
