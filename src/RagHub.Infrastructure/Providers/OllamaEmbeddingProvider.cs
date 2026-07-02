using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RagHub.Core.Interfaces;
using RagHub.Core.Settings;

namespace RagHub.Infrastructure.Providers;

public class OllamaEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly ILogger<OllamaEmbeddingProvider> _logger;

    public string ModelName => _model;
    public int Dimensions { get; private set; }

    public OllamaEmbeddingProvider(HttpClient http, IOptions<RagSettings> opts, ILogger<OllamaEmbeddingProvider> logger)
    {
        _http = http;
        _model = opts.Value.Embedding.OllamaModel;
        _logger = logger;
        Dimensions = 1024;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var result = await EmbedBatchAsync([text], ct);
        return result[0];
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogDebug("Embedding {Count} text(s) via {Model} (first 80 chars: {Preview})",
            texts.Count, _model,
            texts[0].Length > 80 ? texts[0][..80] + "…" : texts[0]);

        // Ollama's embedding runner occasionally returns a transient 500 (NaN in output
        // when its internal buffer is resized under concurrent batch-size churn) — retry once.
        HttpResponseMessage response;
        try
        {
            response = await PostEmbedAsync(texts, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Ollama embed call failed, retrying once");
            response = await PostEmbedAsync(texts, ct);
            response.EnsureSuccessStatusCode();
        }

        var body = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(ct)
            ?? throw new InvalidOperationException("Empty response from Ollama embed endpoint.");

        if (body.Embeddings is null || body.Embeddings.Count != texts.Count)
            throw new InvalidOperationException(
                $"Ollama returned {body.Embeddings?.Count ?? 0} embeddings for {texts.Count} inputs.");

        Dimensions = body.Embeddings[0].Length;
        _logger.LogDebug("Embedded {Count} text(s) in {Ms}ms → dim={Dim}",
            texts.Count, sw.ElapsedMilliseconds, Dimensions);
        return body.Embeddings;
    }

    private Task<HttpResponseMessage> PostEmbedAsync(IReadOnlyList<string> texts, CancellationToken ct) =>
        _http.PostAsJsonAsync("api/embed", new { model = _model, input = texts }, ct);

    private sealed class OllamaEmbedResponse
    {
        public List<float[]>? Embeddings { get; set; }
    }
}
