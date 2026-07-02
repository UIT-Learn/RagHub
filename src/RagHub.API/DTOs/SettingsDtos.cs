using RagHub.Core.Domain;

namespace RagHub.API.DTOs;

public record ChunkingProfileRequest(string Name, string Strategy, int MaxChunkSize, int Overlap);

public record ChunkingProfileResponse(
    int Id, string Name, string Strategy, int MaxChunkSize, int Overlap, DateTime CreatedAt);

public record RetrievalConfigRequest(
    int CandidateK, int FinalN, bool UseHybrid, bool UseReranker, bool UseMultiQuery, int MultiQueryCount);

public record RetrievalConfigResponse(
    int CandidateK, int FinalN, bool UseHybrid, bool UseReranker,
    bool UseMultiQuery, int MultiQueryCount, DateTime UpdatedAt);

public static class SettingsMapper
{
    public static ChunkingProfileResponse ToResponse(ChunkingProfile p) => new(
        p.Id, p.Name, p.Strategy, p.MaxChunkSize, p.Overlap, p.CreatedAt);

    public static RetrievalConfigResponse ToResponse(RetrievalConfig c) => new(
        c.CandidateK, c.FinalN, c.UseHybrid, c.UseReranker, c.UseMultiQuery, c.MultiQueryCount, c.UpdatedAt);
}
