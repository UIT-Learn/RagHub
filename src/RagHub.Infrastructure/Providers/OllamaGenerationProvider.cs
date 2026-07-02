using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RagHub.Core.Interfaces;
using RagHub.Core.Settings;

namespace RagHub.Infrastructure.Providers;

public class OllamaGenerationProvider(
    HttpClient http,
    IOptions<RagSettings> opts,
    ILogger<OllamaGenerationProvider> logger) : IGenerationProvider
{
    private readonly GenerationSettings _cfg = opts.Value.Generation;

    public async IAsyncEnumerable<string> GenerateStreamAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        logger.LogDebug("Generation request → model={Model} num_ctx={NumCtx} think={Think} prompt_chars={Chars}",
            _cfg.OllamaModel, _cfg.NumCtx, _cfg.Think, prompt.Length);

        var sw = Stopwatch.StartNew();
        int tokenCount = 0;

        var request = new HttpRequestMessage(HttpMethod.Post, "api/generate")
        {
            Content = JsonContent.Create(new
            {
                model  = _cfg.OllamaModel,
                prompt,
                stream = true,
                think  = _cfg.Think,
                options = new { num_ctx = _cfg.NumCtx },
            })
        };

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        logger.LogDebug("Generation stream opened in {Ms}ms", sw.ElapsedMilliseconds);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;

            var obj = JsonSerializer.Deserialize<OllamaGenerateChunk>(line,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (obj is null) continue;
            // Skip thinking tokens (non-null Thinking field = internal reasoning, not shown to user)
            if (obj.Thinking is not null) continue;
            if (!string.IsNullOrEmpty(obj.Response))
            {
                tokenCount++;
                yield return obj.Response;
            }
            if (obj.Done)
            {
                logger.LogDebug("Generation complete: {Tokens} tokens in {Ms}ms",
                    tokenCount, sw.ElapsedMilliseconds);
                break;
            }
        }
    }

    private sealed class OllamaGenerateChunk
    {
        public string? Response { get; set; }
        public string? Thinking { get; set; }
        public bool Done { get; set; }
    }
}
