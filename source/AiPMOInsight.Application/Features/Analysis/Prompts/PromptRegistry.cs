using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace AiPMOInsight.Application.Features.Analysis.Prompts;

/// <summary>A prompt and the content-hash version stamped onto findings it produces.</summary>
public sealed record Prompt(string Name, string Content, string Version);

/// <summary>
/// Registry of the LLM agents' prompts, keyed by name and <b>versioned by the hash of their
/// content</b> — so a prompt tweak is traceable to the findings it produced (the hash is the
/// <c>PromptVersion</c> on <see cref="AiPMOInsight.Domain.Findings.Finding"/>). Prompts are files in
/// the repo (embedded in the assembly), never stored in the DB (PRD).
/// </summary>
public sealed class PromptRegistry
{
    private const string ResourcePrefix = "AiPMOInsight.Application.Features.Analysis.Prompts.";
    private const string ResourceSuffix = ".prompt.md";

    private readonly IReadOnlyDictionary<string, Prompt> _prompts;

    public PromptRegistry(IReadOnlyDictionary<string, string> contentByName)
    {
        ArgumentNullException.ThrowIfNull(contentByName);

        _prompts = contentByName.ToDictionary(
            kvp => kvp.Key,
            kvp => new Prompt(kvp.Key, kvp.Value, Hash(kvp.Value)));
    }

    /// <summary>Builds a registry from the prompt files embedded in the Application assembly.</summary>
    public static PromptRegistry FromEmbeddedResources()
    {
        var assembly = typeof(PromptRegistry).Assembly;
        var contentByName = new Dictionary<string, string>();

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal) ||
                !resourceName.EndsWith(ResourceSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            var name = resourceName[ResourcePrefix.Length..^ResourceSuffix.Length];
            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            contentByName[name] = reader.ReadToEnd();
        }

        return new PromptRegistry(contentByName);
    }

    public Prompt Get(string name) =>
        _prompts.TryGetValue(name, out var prompt)
            ? prompt
            : throw new KeyNotFoundException($"No prompt registered under '{name}'.");

    public bool TryGet(string name, out Prompt prompt) => _prompts.TryGetValue(name, out prompt!);

    private static string Hash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return "sha256:" + Convert.ToHexStringLower(bytes);
    }
}
