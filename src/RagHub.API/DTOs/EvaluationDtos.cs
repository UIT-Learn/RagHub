using RagHub.Core.Domain;

namespace RagHub.API.DTOs;

public record ExpectedSourceDto(
    int DocumentId, string DocumentName, string? HeadingPath, int? PageNo, string Snippet);

public record CreateEvaluationRequest(string Question, List<ExpectedSourceDto>? ExpectedSources, string? ExpectedAnswer);

public record EvaluationResponse(
    int Id,
    string Question,
    List<ExpectedSourceDto> ExpectedSources,
    string? ExpectedAnswer,
    List<int> ActualChunkIds,
    string? ActualAnswer,
    double? ReciprocalRank,
    bool? RetrievalPassed,
    bool? CitationPassed,
    DateTime? VerifiedAt,
    DateTime CreatedAt,
    DateTime? RunAt);

public record EvaluationSummaryResponse(
    int TotalQuestions,
    int RunCount,
    int PendingReviewCount,
    double? RecallAtK,
    double? Mrr,
    double? CitationAccuracy);

public static class EvaluationMapper
{
    public static ExpectedSourceDto ToDto(ExpectedSource s) => new(
        s.DocumentId, s.DocumentName, s.HeadingPath, s.PageNo, s.Snippet);

    public static ExpectedSource ToDomain(ExpectedSourceDto d) => new()
    {
        DocumentId   = d.DocumentId,
        DocumentName = d.DocumentName,
        HeadingPath  = d.HeadingPath,
        PageNo       = d.PageNo,
        Snippet      = d.Snippet,
    };

    public static EvaluationResponse ToResponse(Evaluation e) => new(
        e.Id, e.Question, e.ExpectedSources.Select(ToDto).ToList(), e.ExpectedAnswer,
        e.ActualChunkIds, e.ActualAnswer, e.ReciprocalRank,
        e.RetrievalPassed, e.CitationPassed, e.VerifiedAt,
        e.CreatedAt, e.RunAt);
}
