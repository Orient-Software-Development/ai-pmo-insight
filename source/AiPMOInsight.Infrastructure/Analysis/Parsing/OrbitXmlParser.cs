using System.Xml.Linq;
using AiPMOInsight.Application.Features.Analysis.Model;

namespace AiPMOInsight.Infrastructure.Analysis.Parsing;

/// <summary>
/// Parses dummy Orbit-shaped XML (built-in <c>System.Xml.Linq</c>) into RAID records. Each item
/// cites its project + item path so a RAID finding can point back at the exact node.
/// </summary>
internal static class OrbitXmlParser
{
    public static CollectedData Parse(byte[] content)
    {
        using var stream = new MemoryStream(content);
        var document = XDocument.Load(stream);

        var raidItems = new List<RaidItemRecord>();

        foreach (var project in document.Descendants("project"))
        {
            var projectKey = (string?)project.Attribute("key") ?? "UNKNOWN";
            var index = 0;

            foreach (var item in project.Descendants("item"))
            {
                index++;
                var typeText = (string?)item.Attribute("type") ?? nameof(RaidType.Risk);
                var description = item.Value.Trim();

                raidItems.Add(new RaidItemRecord
                {
                    ProjectKey = projectKey,
                    Type = Enum.TryParse<RaidType>(typeText, ignoreCase: true, out var type) ? type : RaidType.Risk,
                    Description = description,
                    Severity = (string?)item.Attribute("severity"),
                    Status = (string?)item.Attribute("status"),
                    Source = new SourceRef(
                        Locator: $"orbit.xml:project[{projectKey}]/raid/item[{index}]",
                        StructuredExcerpt: $"project={projectKey};item={index}",
                        TextSnippet: description),
                });
            }
        }

        return CollectedData.Empty with { RaidItems = raidItems };
    }
}
