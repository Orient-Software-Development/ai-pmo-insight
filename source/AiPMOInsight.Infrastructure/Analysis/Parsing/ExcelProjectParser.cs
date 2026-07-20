using System.Globalization;
using AiPMOInsight.Application.Features.Analysis.Model;
using ClosedXML.Excel;

namespace AiPMOInsight.Infrastructure.Analysis.Parsing;

/// <summary>
/// Parses a dummy Orbit-shaped Excel workbook (ClosedXML) into typed records. Reads the sheets that
/// are present — Projects, Milestones, Budget, Resources, RAID — mapping columns by header name so
/// column order is not load-bearing. Every record carries a <see cref="SourceRef"/> pointing at its
/// sheet + row so downstream findings can cite it.
/// </summary>
internal static class ExcelProjectParser
{
    public static CollectedData Parse(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var workbook = new XLWorkbook(stream);

        return new CollectedData
        {
            Projects = ReadProjects(workbook),
            Milestones = ReadMilestones(workbook),
            BudgetLines = ReadBudget(workbook),
            Assignments = ReadResources(workbook),
            RaidItems = ReadRaid(workbook),
            Decisions = ReadDecisions(workbook),
            ScopeChanges = ReadScope(workbook),
            TimeEntries = ReadTime(workbook),
            Minutes = [],
        };
    }

    // POC time-entries sheet (placeholder shape until the Orbit export convention is agreed).
    private static List<TimeEntryRecord> ReadTime(XLWorkbook wb) =>
        ReadSheet(wb, "Time", (cell, source) => new TimeEntryRecord
        {
            ProjectKey = cell("ProjectKey"),
            Person = cell("Person"),
            HoursLogged = ParseDouble(cell("HoursLogged")) ?? 0,
            Source = source,
        });

    private static List<ProjectRecord> ReadProjects(XLWorkbook wb) =>
        ReadSheet(wb, "Projects", (cell, source) => new ProjectRecord
        {
            Key = cell("Key"),
            Name = cell("Name"),
            PercentComplete = ParseDouble(cell("PercentComplete")),
            LastUpdated = ParseDate(cell("LastUpdated")),
            Customer = NullIfBlank(cell("Customer")),
            Source = source,
        });

    private static List<MilestoneRecord> ReadMilestones(XLWorkbook wb) =>
        ReadSheet(wb, "Milestones", (cell, source) => new MilestoneRecord
        {
            ProjectKey = cell("ProjectKey"),
            Name = cell("Name"),
            DueDate = ParseDate(cell("DueDate")),
            CompletedDate = ParseDate(cell("CompletedDate")),
            Status = NullIfBlank(cell("Status")),
            DependsOn = NullIfBlank(cell("DependsOn")),
            BaselineDate = ParseDate(cell("BaselineDate")),
            IsCritical = ParseBool(cell("IsCritical")),
            Source = source,
        });

    private static List<BudgetLineRecord> ReadBudget(XLWorkbook wb) =>
        ReadSheet(wb, "Budget", (cell, source) => new BudgetLineRecord
        {
            ProjectKey = cell("ProjectKey"),
            Category = cell("Category"),
            Budget = ParseDecimal(cell("Budget")),
            Forecast = ParseDecimal(cell("Forecast")),
            Actual = ParseNullableDecimal(cell("Actual")),
            Currency = NullIfBlank(cell("Currency")),
            Source = source,
        });

    private static List<AssignmentRecord> ReadResources(XLWorkbook wb) =>
        ReadSheet(wb, "Resources", (cell, source) => new AssignmentRecord
        {
            ProjectKey = cell("ProjectKey"),
            Person = cell("Person"),
            Role = cell("Role"),
            AllocationPercent = ParseDouble(cell("AllocationPercent")) ?? 0,
            CapacityPercent = ParseDouble(cell("CapacityPercent")) ?? 100,
            OnLeave = ParseBool(cell("OnLeave")),
            Source = source,
        });

    private static List<RaidItemRecord> ReadRaid(XLWorkbook wb) =>
        ReadSheet(wb, "RAID", (cell, source) => new RaidItemRecord
        {
            ProjectKey = cell("ProjectKey"),
            Type = ParseRaidType(cell("Type")),
            Description = cell("Description"),
            Severity = NullIfBlank(cell("Severity")),
            Status = NullIfBlank(cell("Status")),
            LastUpdated = ParseDate(cell("LastUpdated")),
            Source = source,
        });

    private static List<DecisionRecord> ReadDecisions(XLWorkbook wb) =>
        ReadSheet(wb, "Decisions", (cell, source) => new DecisionRecord
        {
            ProjectKey = cell("ProjectKey"),
            Title = cell("Title"),
            Status = NullIfBlank(cell("Status")),
            Owner = NullIfBlank(cell("Owner")),
            NeededBy = ParseDate(cell("NeededBy")),
            Consequence = NullIfBlank(cell("Consequence")),
            Source = source,
        });

    // POC scope-change sheet (placeholder shape until the Orbit export convention is agreed).
    private static List<ScopeChangeRecord> ReadScope(XLWorkbook wb) =>
        ReadSheet(wb, "Scope", (cell, source) => new ScopeChangeRecord
        {
            ProjectKey = cell("ProjectKey"),
            Title = cell("Title"),
            Type = NullIfBlank(cell("Type")),
            Status = NullIfBlank(cell("Status")),
            EffortImpactPct = ParseNullableDecimal(cell("EffortImpactPct")),
            DateRaised = ParseDate(cell("DateRaised")),
            Source = source,
        });

    /// <summary>
    /// Reads every data row of a sheet (if present) via a header-name column accessor, building one
    /// record per row with a sheet!row source locator.
    /// </summary>
    private static List<T> ReadSheet<T>(XLWorkbook wb, string sheetName, Func<Func<string, string>, SourceRef, T> map)
    {
        var results = new List<T>();
        if (!wb.TryGetWorksheet(sheetName, out var ws))
        {
            return results;
        }

        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var headerCell in ws.Row(1).CellsUsed())
        {
            columns[headerCell.GetString().Trim()] = headerCell.Address.ColumnNumber;
        }

        foreach (var row in ws.RowsUsed().Skip(1))
        {
            string Cell(string header) =>
                columns.TryGetValue(header, out var col) ? row.Cell(col).GetString().Trim() : string.Empty;

            var source = new SourceRef(
                Locator: $"{sheetName}!row{row.RowNumber()}",
                StructuredExcerpt: $"sheet={sheetName};row={row.RowNumber()}");

            results.Add(map(Cell, source));
        }

        return results;
    }

    private static string? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static double? ParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;

    private static decimal ParseDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    private static decimal? ParseNullableDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;

    private static DateTimeOffset? ParseDate(string value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt)
            ? dt
            : null;

    private static bool ParseBool(string value) => bool.TryParse(value, out var b) && b;

    private static RaidType ParseRaidType(string value) =>
        Enum.TryParse<RaidType>(value, ignoreCase: true, out var type) ? type : RaidType.Risk;
}
