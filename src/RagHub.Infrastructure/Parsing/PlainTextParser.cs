using RagHub.Core.Interfaces;

namespace RagHub.Infrastructure.Parsing;

public class PlainTextParser : IDocumentParser
{
    private static readonly HashSet<string> _supported =
        new(StringComparer.OrdinalIgnoreCase) { ".txt", ".md" };

    public bool CanParse(string fileExtension) => _supported.Contains(fileExtension);

    public async Task<ParsedDocument> ParseAsync(string filePath, CancellationToken ct = default)
    {
        string text = await File.ReadAllTextAsync(filePath, ct);
        return new ParsedDocument(text, []);
    }
}
