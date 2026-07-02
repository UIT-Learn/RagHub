namespace RagHub.Core.Interfaces;

public interface IReranker
{
    Task<IReadOnlyList<(int ChunkId, double Score)>> RerankAsync(
        string query,
        IReadOnlyList<(int ChunkId, string Content)> candidates,
        CancellationToken ct = default);
}
