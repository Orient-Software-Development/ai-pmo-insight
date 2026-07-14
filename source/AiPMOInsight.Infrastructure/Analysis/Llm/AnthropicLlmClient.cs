using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic;
using Anthropic.Models.Messages;
using AiPMOInsight.Application.Abstractions;

namespace AiPMOInsight.Infrastructure.Analysis.Llm;

/// <summary>
/// Anthropic Messages API adapter. Recognised by the factory as <c>Provider = "anthropic"</c>.
/// Requests <b>structured JSON output</b> constrained to a schema derived from <c>TOutput</c>
/// (<see cref="JsonSchemaGenerator"/>) and deserialises the returned text block into <c>TOutput</c> —
/// never free-text parsing (issue #27). Model / key / token budget come from the resolved
/// <see cref="LlmProviderOptions"/>. The configured <see cref="LlmProviderOptions.ApiKey"/> is never
/// surfaced in an exception, log line, or telemetry (R3 secret-leak guard).
/// </summary>
public sealed class AnthropicLlmClient : ILlmClient
{
    /// <summary>Model used when the resolved options leave <see cref="LlmProviderOptions.ModelId"/> empty.</summary>
    private const string DefaultModel = "claude-opus-4-8";

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly LlmProviderOptions _options;
    private readonly AnthropicClient _client;

    /// <summary>Production constructor — builds an <see cref="AnthropicClient"/> from the options' key.</summary>
    public AnthropicLlmClient(LlmProviderOptions options)
        : this(options, BuildClient(options))
    {
    }

    /// <summary>Test seam: inject a pre-built client (e.g. one backed by a mock HTTP handler).</summary>
    internal AnthropicLlmClient(LlmProviderOptions options, AnthropicClient client)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    private static AnthropicClient BuildClient(LlmProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new AnthropicClient { ApiKey = options.ApiKey };
    }

    public async Task<TOutput> CompleteAsync<TOutput>(LlmRequest request, CancellationToken cancellationToken)
        where TOutput : notnull
    {
        ArgumentNullException.ThrowIfNull(request);

        var model = string.IsNullOrWhiteSpace(_options.ModelId) ? DefaultModel : _options.ModelId;
        var schema = ToSchemaDictionary(JsonSchemaGenerator.For<TOutput>());

        // Adaptive extended thinking is a per-model capability — Haiku 4.5 rejects it outright,
        // Opus/Sonnet accept and benefit from it. Opt in per agent via
        // LlmProviderOptions.EnableExtendedThinking so the client works across any Claude model.
        ThinkingConfigParam? thinking = null;
        if (_options.EnableExtendedThinking == true)
        {
            thinking = new ThinkingConfigAdaptive();
        }

        var parameters = new MessageCreateParams
        {
            Model = model,
            MaxTokens = _options.PerAnalysisTokenBudget,
            Thinking = thinking,
            OutputConfig = new OutputConfig { Format = new JsonOutputFormat { Schema = schema } },
            Messages = [new() { Role = Role.User, Content = request.Prompt }],
        };

        Message response;
        try
        {
            response = await _client.Messages.Create(parameters, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw; // honour cancellation — never wrap it as a provider failure
        }
        catch (Anthropic.Exceptions.AnthropicApiException ex)
        {
            // R3 secret-leak guard: name the provider + skill, never the ApiKey. The SDK exception
            // (carrying only the server error body + request id) is preserved as the inner exception.
            throw new LlmProviderException(
                $"Anthropic call failed for skill '{request.SkillName}' (provider 'anthropic').", ex);
        }

        var text = response.Content
            .Select(block => block.Value)
            .OfType<TextBlock>()
            .Select(t => t.Text)
            .FirstOrDefault(static s => !string.IsNullOrWhiteSpace(s));

        if (text is null)
        {
            throw new InvalidOperationException(
                $"Anthropic returned no text content for skill '{request.SkillName}'.");
        }

        return JsonSerializer.Deserialize<TOutput>(text, DeserializeOptions)
            ?? throw new InvalidOperationException(
                $"Anthropic response for skill '{request.SkillName}' did not deserialise into " +
                $"{typeof(TOutput).Name}.");
    }

    private static Dictionary<string, JsonElement> ToSchemaDictionary(JsonObject schema)
    {
        using var doc = JsonDocument.Parse(schema.ToJsonString());
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
    }
}
