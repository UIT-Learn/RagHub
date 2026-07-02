namespace RagHub.API.DTOs;

public record SearchRequest(string Query, int? TopK = null);

public record SearchResultItem(
    int ChunkId,
    int DocumentId,
    string DocumentName,
    string Content,
    string? HeadingPath,
    int? PageNo,
    int CharStart,
    int CharEnd,
    double Score);

public record SearchResponse(
    string Query,
    string Pipeline,   // e.g. "dense", "hybrid", "hybrid+rerank"
    IReadOnlyList<SearchResultItem> Results);
