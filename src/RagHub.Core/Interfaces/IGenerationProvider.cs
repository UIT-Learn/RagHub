namespace RagHub.Core.Interfaces;

public interface IGenerationProvider
{
    IAsyncEnumerable<string> GenerateStreamAsync(string prompt, CancellationToken ct = default);
}
