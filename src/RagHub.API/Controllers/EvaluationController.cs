using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RagHub.API.DTOs;
using RagHub.Core.Domain;
using RagHub.Core.Interfaces;
using RagHub.Infrastructure.Chat;
using RagHub.Infrastructure.Persistence;

namespace RagHub.API.Controllers;

[ApiController]
[Route("api/evaluation")]
public class EvaluationController(RagDbContext db, ChatService chat) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await db.Evaluations.OrderByDescending(e => e.CreatedAt).ToListAsync(ct);
        return Ok(items.Select(EvaluationMapper.ToResponse));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEvaluationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { error = "Question must not be empty." });

        var eval = new Evaluation
        {
            Question         = request.Question,
            ExpectedSources  = (request.ExpectedSources ?? []).Select(EvaluationMapper.ToDomain).ToList(),
            ExpectedAnswer   = request.ExpectedAnswer,
        };
        db.Evaluations.Add(eval);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(List), EvaluationMapper.ToResponse(eval));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var eval = await db.Evaluations.FindAsync([id], ct);
        if (eval is null) return NotFound();

        db.Evaluations.Remove(eval);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:int}/run")]
    public async Task<IActionResult> Run(int id, CancellationToken ct)
    {
        var eval = await db.Evaluations.FindAsync([id], ct);
        if (eval is null) return NotFound();

        await RunOneAsync(eval, ct);
        await db.SaveChangesAsync(ct);
        return Ok(EvaluationMapper.ToResponse(eval));
    }

    [HttpPost("run-all")]
    public async Task<IActionResult> RunAll(CancellationToken ct)
    {
        var items = await db.Evaluations.ToListAsync(ct);
        foreach (var eval in items)
            await RunOneAsync(eval, ct);

        await db.SaveChangesAsync(ct);
        return Ok(items.Select(EvaluationMapper.ToResponse));
    }

    [HttpPost("{id:int}/verify")]
    public async Task<IActionResult> Verify(int id, CancellationToken ct)
    {
        var eval = await db.Evaluations.FindAsync([id], ct);
        if (eval is null) return NotFound();

        eval.VerifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(EvaluationMapper.ToResponse(eval));
    }

    [HttpDelete("{id:int}/verify")]
    public async Task<IActionResult> Unverify(int id, CancellationToken ct)
    {
        var eval = await db.Evaluations.FindAsync([id], ct);
        if (eval is null) return NotFound();

        eval.VerifiedAt = null;
        await db.SaveChangesAsync(ct);
        return Ok(EvaluationMapper.ToResponse(eval));
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        int total = await db.Evaluations.CountAsync(ct);

        // Only verified questions count toward the reported benchmark — an unreviewed
        // golden question could have the wrong expected source and silently skew the number.
        var verifiedRun = await db.Evaluations
            .Where(e => e.RunAt != null && e.VerifiedAt != null)
            .ToListAsync(ct);
        int pendingReview = await db.Evaluations
            .CountAsync(e => e.RunAt != null && e.VerifiedAt == null, ct);

        if (verifiedRun.Count == 0)
            return Ok(new EvaluationSummaryResponse(total, 0, pendingReview, null, null, null));

        var scored = verifiedRun.Where(e => e.RetrievalPassed != null).ToList();
        double? recall = scored.Count > 0
            ? scored.Count(e => e.RetrievalPassed == true) / (double)scored.Count
            : null;
        double? mrr = scored.Count > 0
            ? scored.Average(e => e.ReciprocalRank ?? 0)
            : null;
        double? citation = scored.Count > 0
            ? scored.Count(e => e.CitationPassed == true) / (double)scored.Count
            : null;

        return Ok(new EvaluationSummaryResponse(total, verifiedRun.Count, pendingReview, recall, mrr, citation));
    }

    // A retrieved chunk satisfies an expected source if it's from the same document and
    // matches on page, heading, or a content snippet — any one signal is enough, since
    // chunk boundaries shift across reindexes but these don't.
    private static bool Matches(RetrievedChunk chunk, ExpectedSource expected)
    {
        if (chunk.DocumentId != expected.DocumentId) return false;
        if (expected.PageNo is int p && chunk.PageNo == p) return true;
        if (!string.IsNullOrWhiteSpace(expected.HeadingPath) &&
            string.Equals(chunk.HeadingPath, expected.HeadingPath, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!string.IsNullOrWhiteSpace(expected.Snippet) &&
            chunk.Content.Contains(expected.Snippet, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private async Task RunOneAsync(Evaluation eval, CancellationToken ct)
    {
        var chatResult = await chat.QueryAsync(eval.Question, ct);
        var sources    = chatResult.Sources;
        eval.ActualChunkIds = sources.Select(c => c.ChunkId).ToList();

        var answer = new System.Text.StringBuilder();
        await foreach (var token in chatResult.TokenStream.WithCancellation(ct))
            answer.Append(token);
        eval.ActualAnswer = answer.ToString();

        if (eval.ExpectedSources.Count > 0)
        {
            int firstHitRank = -1;
            for (int i = 0; i < sources.Count; i++)
            {
                if (eval.ExpectedSources.Any(es => Matches(sources[i], es)))
                {
                    firstHitRank = i + 1;
                    break;
                }
            }
            eval.ReciprocalRank  = firstHitRank > 0 ? 1.0 / firstHitRank : 0.0;
            eval.RetrievalPassed = firstHitRank > 0;

            var sourceById = sources.ToDictionary(s => s.ChunkId);
            var citedIds   = ChatService.ExtractCitedChunkIds(eval.ActualAnswer);
            eval.CitationPassed = citedIds.Any(id =>
                sourceById.TryGetValue(id, out var c) && eval.ExpectedSources.Any(es => Matches(c, es)));
        }
        else
        {
            eval.ReciprocalRank  = null;
            eval.RetrievalPassed = null;
            eval.CitationPassed  = null;
        }

        eval.RunAt = DateTime.UtcNow;
    }
}
