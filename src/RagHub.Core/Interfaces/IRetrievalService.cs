namespace RagHub.Core.Interfaces;

public record RetrievedChunk(
    int ChunkId,
    int DocumentId,
    string DocumentName,
    string Content,
    string? HeadingPath,
    int? PageNo,
    int CharStart,
    int CharEnd,
    double Score);

public record RetrievalResult(
    IReadOnlyList<RetrievedChunk> Chunks,
    string Pipeline);

public interface IRetrievalService
{
    Task<RetrievalResult> SearchAsync(string query, int? topN, CancellationToken ct = default);
}
