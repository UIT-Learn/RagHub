namespace RagHub.API.DTOs;

public record ChatHistoryTurn(string Role, string Content);

public record ChatQueryRequest(string Query, List<ChatHistoryTurn>? History);

public record FeedbackRequest(
    string Query,
    string Answer,
    List<int> RetrievedChunkIds,
    string Rating);

public record SourceChunk(
    int ChunkId,
    int DocumentId,
    string DocumentName,
    string? HeadingPath,
    int? PageNo,
    int CharStart,
    int CharEnd);
