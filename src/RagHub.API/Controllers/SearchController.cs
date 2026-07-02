using Microsoft.AspNetCore.Mvc;
using RagHub.API.Auth;
using RagHub.API.DTOs;
using RagHub.Core.Interfaces;

namespace RagHub.API.Controllers;

[ApiController]
[Route("api/search")]
[ApiKey]
public class SearchController(IRetrievalService retrieval) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Search([FromBody] SearchRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new { error = "Query must not be empty." });

        var result = await retrieval.SearchAsync(request.Query, request.TopK, ct);

        return Ok(new SearchResponse(
            request.Query,
            result.Pipeline,
            result.Chunks.Select(c => new SearchResultItem(
                c.ChunkId, c.DocumentId, c.DocumentName,
                c.Content, c.HeadingPath, c.PageNo,
                c.CharStart, c.CharEnd, c.Score)).ToList()));
    }
}
