using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using RagHub.Core.Interfaces;

namespace RagHub.Infrastructure.Parsing;

public class DocxParser : IDocumentParser
{
    public bool CanParse(string fileExtension) =>
        fileExtension.Equals(".docx", StringComparison.OrdinalIgnoreCase);

    public Task<ParsedDocument> ParseAsync(string filePath, CancellationToken ct = default)
    {
        var sb = new System.Text.StringBuilder();

        using var doc = WordprocessingDocument.Open(filePath, isEditable: false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null)
            return Task.FromResult(new ParsedDocument(string.Empty, []));

        foreach (var para in body.Elements<Paragraph>())
        {
            ct.ThrowIfCancellationRequested();
            string text = para.InnerText.Trim();
            if (text.Length > 0)
            {
                sb.AppendLine(text);
            }
        }

        // DOCX has no reliable programmatic page numbers; pages are null on chunks.
        return Task.FromResult(new ParsedDocument(sb.ToString(), []));
    }
}
