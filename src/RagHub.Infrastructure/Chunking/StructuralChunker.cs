using System.Text.RegularExpressions;
using RagHub.Core.Domain;
using RagHub.Core.Interfaces;

namespace RagHub.Infrastructure.Chunking;

// Pure function of (document, docType, profile) — no constructor-injected global config.
public partial class StructuralChunker : IChunker
{
    // --- doc-type auto-detection ---

    public static string DetectDocType(string text)
    {
        if (ApiVerbPattern().IsMatch(text)) return "api";
        if (NumberedHeadingPattern().IsMatch(text)) return "policy";
        return "technical";
    }

    // --- main entry ---

    public IReadOnlyList<ChunkResult> Chunk(ParsedDocument document, string docType, ChunkingProfile profile)
    {
        string type = string.IsNullOrWhiteSpace(profile.Strategy) || profile.Strategy == "auto"
            ? (string.IsNullOrWhiteSpace(docType) ? DetectDocType(document.FullText) : docType)
            : profile.Strategy;

        var sections = type switch
        {
            "policy" => SplitByNumberedHeadings(document.FullText),
            "api"    => SplitByApiEndpoints(document.FullText),
            "legal"  => SplitByLegalClauses(document.FullText),
            "faq"    => SplitByFaqEntries(document.FullText),
            "fixed"  => [(string.Empty, document.FullText, 0)],
            _        => SplitByMarkdownHeadings(document.FullText),
        };

        var results = new List<ChunkResult>();
        int index = 0;
        foreach (var (heading, content, charStart) in sections)
        {
            foreach (var chunk in RecursiveSplit(content, charStart, profile.MaxChunkSize, profile.Overlap))
            {
                int? pageNo = ResolvePageNo(chunk.start, document.Pages);
                results.Add(new ChunkResult(
                    index++,
                    chunk.text,
                    heading,
                    pageNo,
                    chunk.start,
                    chunk.end,
                    EstimateTokens(chunk.text)));
            }
        }
        return results;
    }

    // --- splitters ---

    private static List<(string heading, string content, int charStart)> SplitByMarkdownHeadings(string text)
    {
        var matches = MarkdownHeadingPattern().Matches(text);
        return SliceAtMatches(text, matches, m => m.Value.TrimStart('#').Trim());
    }

    private static List<(string heading, string content, int charStart)> SplitByNumberedHeadings(string text)
    {
        var matches = NumberedHeadingPattern().Matches(text);
        return SliceAtMatches(text, matches, m => m.Value.Trim());
    }

    private static List<(string heading, string content, int charStart)> SplitByApiEndpoints(string text)
    {
        var matches = ApiVerbPattern().Matches(text);
        return SliceAtMatches(text, matches, m => m.Value.Trim());
    }

    // Contracts/legal docs: "Article 3", "Section 4.2", "Clause 1" — broader than the
    // pure-numeric policy heading pattern, since legal numbering is usually word-prefixed.
    private static List<(string heading, string content, int charStart)> SplitByLegalClauses(string text)
    {
        var matches = LegalClausePattern().Matches(text);
        return SliceAtMatches(text, matches, m => m.Value.Trim());
    }

    // FAQ docs: one section per "Q:"/"Question N:" line, content runs to the next one.
    private static List<(string heading, string content, int charStart)> SplitByFaqEntries(string text)
    {
        var matches = FaqQuestionPattern().Matches(text);
        return SliceAtMatches(text, matches, m => m.Value.Trim());
    }

    private static List<(string heading, string content, int charStart)> SliceAtMatches(
        string text, MatchCollection matches, Func<Match, string> headingSelector)
    {
        var sections = new List<(string, string, int)>();

        if (matches.Count == 0)
        {
            if (text.Length > 0) sections.Add((string.Empty, text, 0));
            return sections;
        }

        // text before first heading
        if (matches[0].Index > 0)
            sections.Add((string.Empty, text[..matches[0].Index].Trim(), 0));

        for (int i = 0; i < matches.Count; i++)
        {
            int start = matches[i].Index;
            int end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
            string content = text[start..end].Trim();
            sections.Add((headingSelector(matches[i]), content, start));
        }

        return sections;
    }

    // --- recursive size-cap split ---

    private IEnumerable<(string text, int start, int end)> RecursiveSplit(
        string text, int baseOffset, int maxSize, int overlap)
    {
        if (text.Length <= maxSize)
        {
            yield return (text, baseOffset, baseOffset + text.Length);
            yield break;
        }

        // split on double-newline (paragraph), then on single newline, then hard-cut
        var separators = new[] { "\n\n", "\n", ". " };
        foreach (string sep in separators)
        {
            var parts = SplitWithOverlap(text, sep, maxSize, overlap);
            if (parts.Count > 1)
            {
                int cursor = baseOffset;
                foreach (string part in parts)
                {
                    // Search back by overlap so we find where the (possibly overlapping) part starts
                    int searchFrom = Math.Clamp(cursor - baseOffset - overlap, 0, text.Length);
                    int partStart = text.IndexOf(part, searchFrom, StringComparison.Ordinal);
                    int absStart = baseOffset + (partStart >= 0 ? partStart : cursor - baseOffset);
                    yield return (part, absStart, absStart + part.Length);
                    cursor = absStart + part.Length;
                }
                yield break;
            }
        }

        // hard cut as last resort
        for (int i = 0; i < text.Length; i += maxSize - overlap)
        {
            int len = Math.Min(maxSize, text.Length - i);
            yield return (text.Substring(i, len), baseOffset + i, baseOffset + i + len);
        }
    }

    private static List<string> SplitWithOverlap(string text, string separator, int maxSize, int overlap)
    {
        var parts = text.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        var current = new System.Text.StringBuilder();

        foreach (string part in parts)
        {
            if (current.Length + separator.Length + part.Length > maxSize && current.Length > 0)
            {
                chunks.Add(current.ToString().Trim());
                // carry overlap from end of current
                string tail = current.Length > overlap ? current.ToString()[^overlap..] : current.ToString();
                current.Clear();
                current.Append(tail);
                current.Append(separator);
            }
            current.Append(part);
            current.Append(separator);
        }

        if (current.Length > 0) chunks.Add(current.ToString().Trim());
        return chunks;
    }

    // --- helpers ---

    private static int? ResolvePageNo(int charStart, IReadOnlyList<PageSpan> pages)
    {
        if (pages.Count == 0) return null;
        foreach (var span in pages)
            if (charStart >= span.CharStart && charStart < span.CharEnd)
                return span.PageNo;
        return pages[^1].PageNo;
    }

    private static int EstimateTokens(string text) => Math.Max(1, text.Length / 4);

    // --- compiled regexes ---

    [GeneratedRegex(@"^#{1,6}\s+.+", RegexOptions.Multiline)]
    private static partial Regex MarkdownHeadingPattern();

    [GeneratedRegex(@"^\d+(\.\d+)*\s+\S.{0,80}$", RegexOptions.Multiline)]
    private static partial Regex NumberedHeadingPattern();

    [GeneratedRegex(@"^(GET|POST|PUT|PATCH|DELETE)\s+/\S*", RegexOptions.Multiline)]
    private static partial Regex ApiVerbPattern();

    [GeneratedRegex(@"^(Article|Section|Clause)\s+\d+(\.\d+)*\b.{0,80}$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex LegalClausePattern();

    [GeneratedRegex(@"^(Q\d*[:.]|Question\s*\d*[:.])\s*.{0,200}$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex FaqQuestionPattern();
}
