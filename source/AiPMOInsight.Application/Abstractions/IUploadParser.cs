using AiPMOInsight.Application.Features.Analysis.Model;

namespace AiPMOInsight.Application.Abstractions;

/// <summary>
/// Port for the Data Collector's file parsing (agent #1). Turns an upload's raw bytes into typed
/// records grouped as <see cref="CollectedData"/>. Implemented in Infrastructure (ClosedXML for
/// Excel, <c>System.Xml</c> for Orbit XML, OpenXml for <c>.docx</c> minutes) so the vendor
/// libraries never leak into Application. Parsing is deterministic — no LLM.
/// </summary>
public interface IUploadParser
{
    /// <summary>Parses <paramref name="content"/> into typed records, dispatching on the file type.</summary>
    CollectedData Parse(string fileName, byte[] content);
}
