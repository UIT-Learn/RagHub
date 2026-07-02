using RagHub.Core.Domain;

namespace RagHub.API.DTOs;

public record UploadDocumentRequest(string Category, string? DocType, int? ChunkingProfileId);

public record ReindexRequest(int? ChunkingProfileId);

public record DocumentResponse(
    int Id,
    string Name,
    string Category,
    string Type,
    string Status,
    int ChunkCount,
    string? EmbeddingModel,
    int? EmbeddingDim,
    int? ChunkingProfileId,
    int? ChunkSizeUsed,
    int? OverlapUsed,
    string? ErrorMessage,
    DateTime UploadedAt);

public record ChunkResponse(
    int Id,
    int ChunkIndex,
    string Content,
    string? HeadingPath,
    int? PageNo,
    int CharStart,
    int CharEnd,
    int TokenCount,
    string? EmbeddingModel);

// Portable bundle for offline/edge use: chunks + embeddings + the exact model that
// produced them, so a downstream consumer can rebuild a local vector store without
// re-embedding (and without silently mixing in a different model's vectors).
public record ExportChunk(
    int ChunkIndex,
    string Content,
    string? HeadingPath,
    int? PageNo,
    int CharStart,
    int CharEnd,
    int TokenCount,
    float[]? Embedding);

public record ExportBundle(
    int DocumentId,
    string Name,
    string Category,
    string Type,
    string? EmbeddingModel,
    int? EmbeddingDim,
    DateTime ExportedAt,
    IReadOnlyList<ExportChunk> Chunks);

public static class DocumentMapper
{
    public static DocumentResponse ToResponse(Document d) => new(
        d.Id, d.Name, d.Category, d.Type, d.Status.ToString(),
        d.ChunkCount, d.EmbeddingModel, d.EmbeddingDim,
        d.ChunkingProfileId, d.ChunkSizeUsed, d.OverlapUsed,
        d.ErrorMessage, d.UploadedAt);

    public static ChunkResponse ToResponse(Chunk c) => new(
        c.Id, c.ChunkIndex, c.Content, c.HeadingPath, c.PageNo,
        c.CharStart, c.CharEnd, c.TokenCount, c.EmbeddingModel);

    public static ExportBundle ToExportBundle(Document d, IReadOnlyList<Chunk> chunks) => new(
        d.Id, d.Name, d.Category, d.Type, d.EmbeddingModel, d.EmbeddingDim, DateTime.UtcNow,
        chunks.Select(c => new ExportChunk(
            c.ChunkIndex, c.Content, c.HeadingPath, c.PageNo,
            c.CharStart, c.CharEnd, c.TokenCount,
            c.Embedding?.ToArray())).ToList());
}
