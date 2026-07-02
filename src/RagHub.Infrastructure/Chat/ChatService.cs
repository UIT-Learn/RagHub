using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RagHub.Core.Interfaces;

namespace RagHub.Infrastructure.Chat;

public record ChatTurn(string Role, string Content); // Role: "user" | "assistant"

public record ChatQueryResult(
    IReadOnlyList<RetrievedChunk> Sources,
    IAsyncEnumerable<string> TokenStream);

public partial class ChatService(
    IRetrievalService retrieval,
    IGenerationProvider generator,
    ILogger<ChatService> logger)
{
    // ~12 000 token budget for context (each token ≈ 4 chars)
    private const int ContextCharBudget = 48_000;
    private const int MaxHistoryTurns = 6; // last N turns kept for condensation + prompt context

    public Task<ChatQueryResult> QueryAsync(string query, CancellationToken ct) =>
        QueryAsync(query, [], ct);

    public async Task<ChatQueryResult> QueryAsync(string query, IReadOnlyList<ChatTurn> history, CancellationToken ct)
    {
        logger.LogInformation("Chat query: {Query} (history: {N} turns)", query, history.Count);

        var recentHistory = history.Count > MaxHistoryTurns
            ? history.Skip(history.Count - MaxHistoryTurns).ToList()
            : history;

        // Resolve references from history + fix obvious typos/grammar — retrieval only, never shown to the user.
        string retrievalQuery = query;
        if (recentHistory.Count > 0)
        {
            try
            {
                retrievalQuery = await CondenseQueryAsync(query, recentHistory, ct);
                logger.LogInformation("Condensed query: \"{Original}\" → \"{Condensed}\"", query, retrievalQuery);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Query condensation failed — using original query for retrieval");
            }
        }

        var result = await retrieval.SearchAsync(retrievalQuery, null, ct);
        var chunks = result.Chunks;

        if (chunks.Count == 0)
        {
            logger.LogWarning("No chunks retrieved for query — returning no-docs message");
            return new ChatQueryResult([], NoDocsStream());
        }

        var prompt = BuildPrompt(query, recentHistory, chunks);
        logger.LogInformation("Prompt built: {Chars} chars covering {N} chunks from docs: {Docs}",
            prompt.Length, chunks.Count,
            string.Join(", ", chunks.Select(c => c.DocumentName).Distinct()));
        logger.LogDebug("Full prompt:\n{Prompt}", prompt);

        var stream = generator.GenerateStreamAsync(prompt, ct);
        return new ChatQueryResult(chunks, stream);
    }

    private static async IAsyncEnumerable<string> NoDocsStream()
    {
        yield return "No indexed content was found matching your query. " +
                     "Please ensure documents are fully indexed (status: Indexed) before querying, " +
                     "or try rephrasing your question.";
        await Task.CompletedTask;
    }

    // --- history-aware query condensation (resolves references + normalizes typos/grammar) ---

    private async Task<string> CondenseQueryAsync(string query, IReadOnlyList<ChatTurn> history, CancellationToken ct)
    {
        var historyText = new StringBuilder();
        foreach (var turn in history)
            historyText.AppendLine($"{turn.Role}: {turn.Content}");

        string prompt = $"""
            Given the conversation history below and a new user message, rewrite the new message into a
            standalone question that can be understood without the history.
            - Resolve pronouns/references (e.g. "it", "that one", "cái đó") using the conversation history.
            - Fix obvious typos and grammar issues.
            - Preserve the original language and intent — do not translate.
            - If the message is already standalone and clear, return it unchanged.
            - Output ONLY the rewritten question. Do not answer it. Do not add quotes or explanation.

            Conversation history:
            {historyText}

            New message: {query}

            Rewritten standalone question:
            """;

        var sb = new StringBuilder();
        await foreach (var token in generator.GenerateStreamAsync(prompt, ct))
            sb.Append(token);

        string rewritten = sb.ToString().Trim().Trim('"');
        return rewritten.Length > 0 ? rewritten : query;
    }

    // --- prompt construction ---

    private static string BuildPrompt(string query, IReadOnlyList<ChatTurn> history, IReadOnlyList<RetrievedChunk> chunks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""
            You are a knowledge assistant. Answer the user's question using ONLY the information provided in the context below.
            - If the answer is not in the context, respond exactly with: "I don't have information about this in the provided documents."
            - Cite sources inline using [chunk:ID] notation (e.g. [chunk:9]).
            - Be accurate. Be concise for simple questions, but thorough when the question asks to list or enumerate items.
            - IMPORTANT: Always respond in the same language as the user's question.
            """);

        if (history.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Conversation so far:");
            foreach (var turn in history)
                sb.AppendLine($"{turn.Role}: {turn.Content}");
        }

        sb.AppendLine();
        sb.AppendLine("Context:");
        sb.AppendLine("---");

        int budget = ContextCharBudget;
        foreach (var c in chunks)
        {
            if (budget <= 0) break;
            string header = $"[chunk:{c.ChunkId}]";
            if (c.HeadingPath is not null)
                header += $" ({c.DocumentName} > {c.HeadingPath})";
            string block = $"{header}\n{c.Content}\n\n";
            sb.Append(block[..Math.Min(block.Length, budget)]);
            budget -= block.Length;
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"Question: {query}");
        sb.AppendLine();
        sb.AppendLine("Answer:");
        return sb.ToString();
    }

    // --- citation extraction from accumulated answer text ---

    public static IReadOnlyList<int> ExtractCitedChunkIds(string answerText)
    {
        return CitationPattern()
            .Matches(answerText)
            .Select(m => int.Parse(m.Groups[1].Value))
            .Distinct()
            .ToList();
    }

    [GeneratedRegex(@"\[chunk:(\d+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex CitationPattern();
}
