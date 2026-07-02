using Pgvector;

namespace RagHub.Core.Domain;

public class Chunk
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public Document Document { get; set; } = null!;

    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? HeadingPath { get; set; }
    public int? PageNo { get; set; }
    public int CharStart { get; set; }
    public int CharEnd { get; set; }
    public int TokenCount { get; set; }

    public string? EmbeddingModel { get; set; }
    public int? EmbeddingDim { get; set; }
    public Vector? Embedding { get; set; }
}
