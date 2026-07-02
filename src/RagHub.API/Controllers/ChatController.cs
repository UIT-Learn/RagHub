using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RagHub.API.Auth;
using RagHub.API.DTOs;
using RagHub.Core.Domain;
using RagHub.Infrastructure.Chat;
using RagHub.Infrastructure.Persistence;

namespace RagHub.API.Controllers;

[ApiController]
[Route("api/chat")]
[ApiKey]
public class ChatController(ChatService chat, RagDbContext db) : ControllerBase
{
    private static readonly JsonSerializerOptions _json =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// SSE stream: data:{type:"token",content:"..."} per token,
    /// then data:{type:"sources",chunks:[...]} with cited chunks,
    /// then data:[DONE]
    /// </summary>
    [HttpPost("query")]
    public async Task StreamQuery([FromBody] ChatQueryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            Response.StatusCode = 400;
            await Response.WriteAsJsonAsync(new { error = "Query must not be empty." }, ct);
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var history  = (request.History ?? []).Select(h => new ChatTurn(h.Role, h.Content)).ToList();
        var result   = await chat.QueryAsync(request.Query, history, ct);
        var sourceMap = result.Sources.ToDictionary(s => s.ChunkId);

        var fullAnswer = new StringBuilder();

        try
        {
            await foreach (var token in result.TokenStream.WithCancellation(ct))
            {
                fullAnswer.Append(token);
                await WriteEventAsync("token", new { content = token }, ct);
            }
        }
        catch (OperationCanceledException) { return; }

        // Resolve cited chunks from the answer text
        var citedIds = ChatService.ExtractCitedChunkIds(fullAnswer.ToString());
        var cited    = citedIds
            .Where(id => sourceMap.ContainsKey(id))
            .Select(id => sourceMap[id])
            .Select(c => new SourceChunk(c.ChunkId, c.DocumentId, c.DocumentName,
                                         c.HeadingPath, c.PageNo, c.CharStart, c.CharEnd))
            .ToList();

        // Fall back to all retrieved chunks if model cited nothing
        if (cited.Count == 0)
        {
            cited = result.Sources
                .Select(c => new SourceChunk(c.ChunkId, c.DocumentId, c.DocumentName,
                                             c.HeadingPath, c.PageNo, c.CharStart, c.CharEnd))
                .ToList();
        }

        await WriteEventAsync("sources", new { chunks = cited }, ct);
        await Response.WriteAsync("data: [DONE]\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    [HttpPost("feedback")]
    public async Task<IActionResult> Feedback([FromBody] FeedbackRequest request, CancellationToken ct)
    {
        var feedback = new Feedback
        {
            Query             = request.Query,
            Answer            = request.Answer,
            RetrievedChunkIds = request.RetrievedChunkIds,
            Rating            = request.Rating,
            CreatedAt         = DateTime.UtcNow,
        };
        db.Feedbacks.Add(feedback);
        await db.SaveChangesAsync(ct);
        return Created($"/api/chat/feedback/{feedback.Id}", new { id = feedback.Id });
    }

    private async Task WriteEventAsync(string eventType, object payload, CancellationToken ct)
    {
        var data = JsonSerializer.Serialize(
            new { type = eventType, data = payload }, _json);
        await Response.WriteAsync($"data: {data}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}
