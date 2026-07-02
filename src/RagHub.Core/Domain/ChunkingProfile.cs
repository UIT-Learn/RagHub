namespace RagHub.Core.Domain;

public class ChunkingProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // "auto" | "policy" | "technical" | "api" — "auto" detects from heading patterns.
    public string Strategy { get; set; } = "auto";
    public int MaxChunkSize { get; set; } = 1500;
    public int Overlap { get; set; } = 100;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
