using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AiPMOInsight.Api.Tests.Fixtures;

/// <summary>
/// Builds dummy Orbit-shaped fixtures in memory (no checked-in binaries) so the Data Collector's
/// real ClosedXML / OpenXml / System.Xml parsers are exercised against genuine OOXML.
/// </summary>
internal static class OrbitFixtureBuilder
{
    /// <summary>
    /// A workbook covering the tabular categories: Projects, Milestones, Budget, Resources, RAID.
    /// ALPHA is a RED-ish project (forecast over budget, milestone slipped, over-allocated resource).
    /// </summary>
    public static byte[] Workbook()
    {
        using var wb = new XLWorkbook();

        var projects = wb.AddWorksheet("Projects");
        WriteRow(projects, 1, "Key", "Name", "PercentComplete", "LastUpdated");
        WriteRow(projects, 2, "ALPHA", "Alpha Platform", "45", "2026-06-20");

        var milestones = wb.AddWorksheet("Milestones");
        WriteRow(milestones, 1, "ProjectKey", "Name", "DueDate", "CompletedDate", "Status", "DependsOn");
        WriteRow(milestones, 2, "ALPHA", "Design complete", "2026-05-01", "2026-06-10", "Done", "");
        WriteRow(milestones, 3, "ALPHA", "Beta release", "2026-06-15", "", "In progress", "Design complete");

        var budget = wb.AddWorksheet("Budget");
        WriteRow(budget, 1, "ProjectKey", "Category", "Budget", "Forecast", "Actual");
        WriteRow(budget, 2, "ALPHA", "Development", "100000", "118000", "60000");

        var resources = wb.AddWorksheet("Resources");
        WriteRow(resources, 1, "ProjectKey", "Person", "Role", "AllocationPercent", "CapacityPercent", "OnLeave");
        WriteRow(resources, 2, "ALPHA", "Sam Lee", "Engineer", "120", "100", "false");

        var raid = wb.AddWorksheet("RAID");
        WriteRow(raid, 1, "ProjectKey", "Type", "Description", "Severity", "Status");
        WriteRow(raid, 2, "ALPHA", "Risk", "Vendor API may slip", "High", "Open");

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>Orbit-shaped XML carrying RAID items (an alternative RAID source).</summary>
    public static byte[] OrbitXml() =>
        System.Text.Encoding.UTF8.GetBytes(
            """
            <orbit>
              <project key="ALPHA">
                <raid>
                  <item type="Issue" severity="High" status="Open">Integration environment unavailable</item>
                  <item type="Dependency" severity="Medium" status="Open">Awaiting vendor SDK</item>
                </raid>
              </project>
            </orbit>
            """);

    /// <summary>A .docx of meeting minutes for the LLM (#4) minutes-extraction path.</summary>
    public static byte[] MinutesDocx(string projectKey = "ALPHA")
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body(
                Para($"Project: {projectKey}"),
                Para("Minutes 2026-06-15"),
                Para("Vendor flagged a possible two-week slip on the API integration."),
                Para("Team raised concern about test-environment stability.")));
        }

        return ms.ToArray();
    }

    private static Paragraph Para(string text) => new(new Run(new Text(text)));

    private static void WriteRow(IXLWorksheet ws, int row, params string[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            ws.Cell(row, i + 1).Value = values[i];
        }
    }
}
