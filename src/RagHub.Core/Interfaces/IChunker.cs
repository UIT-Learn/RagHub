using RagHub.Core.Domain;

namespace RagHub.Core.Interfaces;

public record ChunkResult(
    int ChunkIndex,
    string Content,
    string? HeadingPath,
    int? PageNo,
    int CharStart,
    int CharEnd,
    int TokenCount);

public interface IChunker
{
    // Pure function of (document, docType, profile) — no hidden global config state.
    IReadOnlyList<ChunkResult> Chunk(ParsedDocument document, string docType, ChunkingProfile profile);
}
