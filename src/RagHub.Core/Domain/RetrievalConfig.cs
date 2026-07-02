namespace RagHub.Core.Domain;

// Single-row table — live-tunable retrieval knobs, no reindex required to take effect.
// Named "Config" (not "Settings") to avoid clashing with RagHub.Core.Settings.RetrievalSettings
// (the appsettings.json-bound POCO used only as the seed/fallback default).
public class RetrievalConfig
{
    public int Id { get; set; }
    public int CandidateK { get; set; } = 20;
    public int FinalN { get; set; } = 5;
    public bool UseHybrid { get; set; } = true;
    public bool UseReranker { get; set; } = true;
    public bool UseMultiQuery { get; set; } = false;
    public int MultiQueryCount { get; set; } = 3;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
