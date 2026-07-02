using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RagHub.Core.Interfaces;
using RagHub.Core.Settings;

namespace RagHub.Infrastructure.Providers;

public class OpenAiGenerationProvider(HttpClient http, IOptions<RagSettings> opts) : IGenerationProvider
{
    private readonly string _model = opts.Value.Generation.OpenAiModel;

    public async IAsyncEnumerable<string> GenerateStreamAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        {
            Content = JsonContent.Create(new
            {
                model    = _model,
                stream   = true,
                messages = new[] { new { role = "user", content = prompt } },
            })
        };

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") break;

            var doc = JsonDocument.Parse(data);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("delta")
                .TryGetProperty("content", out var c) ? c.GetString() : null;

            if (!string.IsNullOrEmpty(content))
                yield return content;
        }
    }
}
