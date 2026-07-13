using AiPMOInsight.Application.Abstractions;
using AiPMOInsight.Application.Features.Analysis.Model;

namespace AiPMOInsight.Infrastructure.Analysis.Parsing;

/// <summary>
/// The Data Collector's parser (#1). Dispatches on file type to a format-specific parser and returns
/// typed records. Unknown/unsupported types yield <see cref="CollectedData.Empty"/> rather than
/// throwing, so an analysis run degrades gracefully (Data Quality flags the absence of data).
/// </summary>
public sealed class UploadParser : IUploadParser
{
    public CollectedData Parse(string fileName, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".xlsx" or ".xlsm" => ExcelProjectParser.Parse(content),
            ".xml" => OrbitXmlParser.Parse(content),
            ".docx" => DocxMinutesParser.Parse(content),
            _ => CollectedData.Empty,
        };
    }
}
