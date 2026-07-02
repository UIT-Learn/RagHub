namespace RagHub.Core.Interfaces;

public record PageSpan(int PageNo, int CharStart, int CharEnd);

public record ParsedDocument(string FullText, IReadOnlyList<PageSpan> Pages);

public interface IDocumentParser
{
    bool CanParse(string fileExtension);
    Task<ParsedDocument> ParseAsync(string filePath, CancellationToken ct = default);
}
