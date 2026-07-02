using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using RagHub.Core.Interfaces;

namespace RagHub.Infrastructure.Providers;

public class HttpReranker(HttpClient http, ILogger<HttpReranker> logger) : IReranker
{
    public async Task<IReadOnlyList<(int ChunkId, double Score)>> RerankAsync(
        string query,
        IReadOnlyList<(int ChunkId, string Content)> candidates,
        CancellationToken ct = default)
    {
        logger.LogDebug("Reranking {N} candidates for query: {Query}", candidates.Count, query);
        var response = await http.PostAsJsonAsync("rerank", new
        {
            query,
            candidates = candidates.Select(c => new { id = c.ChunkId, text = c.Content }).ToList(),
        }, ct);

        response.EnsureSuccessStatusCode();

        var results = await response.Content.ReadFromJsonAsync<List<RerankResult>>(ct)
            ?? throw new InvalidOperationException("Empty response from reranker sidecar.");

        return results
            .OrderByDescending(r => r.Score)
            .Select(r => (r.Id, (double)r.Score))
            .ToList();
    }

    private sealed record RerankResult(int Id, float Score);
}
