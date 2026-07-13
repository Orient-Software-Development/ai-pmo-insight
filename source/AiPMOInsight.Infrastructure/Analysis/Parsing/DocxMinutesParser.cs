using System.Text.RegularExpressions;
using AiPMOInsight.Application.Features.Analysis.Model;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AiPMOInsight.Infrastructure.Analysis.Parsing;

/// <summary>
/// Parses a <c>.docx</c> of meeting minutes (OpenXml) into <see cref="MinuteEntryRecord"/> entries —
/// one per non-empty paragraph. A leading "Project: X" line sets the project key and the first
/// yyyy-MM-dd token found sets the meeting date. Each entry cites its paragraph and carries its text
/// as the snippet, so the LLM (#4) minutes path and its citations have real content to work from.
/// </summary>
internal static partial class DocxMinutesParser
{
    public static CollectedData Parse(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var document = WordprocessingDocument.Open(stream, isEditable: false);

        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return CollectedData.Empty;
        }

        var paragraphs = body.Descendants<Paragraph>()
            .Select(p => p.InnerText.Trim())
            .Where(text => text.Length > 0)
            .ToList();

        var projectKey = "UNKNOWN";
        DateTimeOffset? date = null;
        foreach (var text in paragraphs)
        {
            if (text.StartsWith("Project:", StringComparison.OrdinalIgnoreCase))
            {
                projectKey = text["Project:".Length..].Trim();
            }

            if (date is null && DatePattern().Match(text) is { Success: true } match &&
                DateTimeOffset.TryParse(match.Value, out var parsed))
            {
                date = parsed;
            }
        }

        var minutes = new List<MinuteEntryRecord>();
        var index = 0;
        foreach (var text in paragraphs)
        {
            index++;
            if (text.StartsWith("Project:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            minutes.Add(new MinuteEntryRecord
            {
                ProjectKey = projectKey,
                Date = date,
                Text = text,
                Source = new SourceRef(
                    Locator: $"minutes.docx:para[{index}]",
                    StructuredExcerpt: $"para={index}",
                    TextSnippet: text),
            });
        }

        return CollectedData.Empty with { Minutes = minutes };
    }

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}")]
    private static partial Regex DatePattern();
}
