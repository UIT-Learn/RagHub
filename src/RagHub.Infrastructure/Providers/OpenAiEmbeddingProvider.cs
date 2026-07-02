using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using RagHub.Core.Interfaces;
using RagHub.Core.Settings;

namespace RagHub.Infrastructure.Providers;

public class OpenAiEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _http;
    private readonly string _model;

    public string ModelName => _model;
    public int Dimensions => 1536;

    public OpenAiEmbeddingProvider(HttpClient http, IOptions<RagSettings> opts)
    {
        _http = http;
        _model = opts.Value.Embedding.OpenAiModel;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var result = await EmbedBatchAsync([text], ct);
        return result[0];
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("v1/embeddings", new
        {
            model = _model,
            input = texts,
        }, ct);

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<OpenAiEmbedResponse>(ct)
            ?? throw new InvalidOperationException("Empty response from OpenAI embed endpoint.");

        return body.Data
            .OrderBy(d => d.Index)
            .Select(d => d.Embedding)
            .ToList();
    }

    private sealed record OpenAiEmbedData(int Index, float[] Embedding);
    private sealed record OpenAiEmbedResponse(List<OpenAiEmbedData> Data);
}
