using RagHub.Core.Interfaces;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace RagHub.Infrastructure.Parsing;

// Scanned/image PDFs are out of scope — only text-layer PDFs are supported.
public class PdfParser : IDocumentParser
{
    public bool CanParse(string fileExtension) =>
        fileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    public Task<ParsedDocument> ParseAsync(string filePath, CancellationToken ct = default)
    {
        var pageSpans = new List<PageSpan>();
        var fullText = new System.Text.StringBuilder();

        using var doc = PdfDocument.Open(filePath);
        foreach (Page page in doc.GetPages())
        {
            ct.ThrowIfCancellationRequested();

            int charStart = fullText.Length;
            string pageText = string.Join(" ", page.GetWords().Select(w => w.Text));

            if (pageText.Length > 0)
            {
                fullText.Append(pageText);
                fullText.Append('\n');
            }

            pageSpans.Add(new PageSpan(page.Number, charStart, fullText.Length));
        }

        return Task.FromResult(new ParsedDocument(fullText.ToString(), pageSpans));
    }
}
