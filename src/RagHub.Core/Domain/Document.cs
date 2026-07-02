namespace RagHub.Core.Domain;

public class Document
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
    public int ChunkCount { get; set; }
    public string? EmbeddingModel { get; set; }
    public int? EmbeddingDim { get; set; }
    public string? Owner { get; set; }
    public string? Department { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
    public string? FilePath { get; set; }

    // Chunking profile used to produce the current chunk set — null until first index.
    public int? ChunkingProfileId { get; set; }
    public ChunkingProfile? ChunkingProfile { get; set; }
    public int? ChunkSizeUsed { get; set; }
    public int? OverlapUsed { get; set; }

    public ICollection<Chunk> Chunks { get; set; } = [];
}
