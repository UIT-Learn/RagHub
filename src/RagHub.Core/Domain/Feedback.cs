namespace RagHub.Core.Domain;

public class Feedback
{
    public int Id { get; set; }
    public string Query { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public List<int> RetrievedChunkIds { get; set; } = [];
    public string Rating { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
