namespace RagHub.Core.Domain;

// Snapshot of where the answer lives — not a chunk FK, so it survives a reindex
// (chunking profile changes regenerate chunk rows with new auto-increment ids).
public class ExpectedSource
{
    public int DocumentId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string? HeadingPath { get; set; }
    public int? PageNo { get; set; }
    public string Snippet { get; set; } = string.Empty;
}

public class Evaluation
{
    public int Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public List<ExpectedSource> ExpectedSources { get; set; } = [];
    public string? ExpectedAnswer { get; set; }
    public List<int> ActualChunkIds { get; set; } = [];
    public string? ActualAnswer { get; set; }
    public double? ReciprocalRank { get; set; }
    public bool? RetrievalPassed { get; set; }
    public bool? CitationPassed { get; set; }

    // Human sign-off gate — unverified questions can still be run for debugging,
    // but are excluded from the reported Summary metrics.
    public DateTime? VerifiedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RunAt { get; set; }
}
