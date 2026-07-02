using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using RagHub.Core.Domain;
using RagHub.Core.Interfaces;
using RagHub.Core.Settings;
using RagHub.Infrastructure.Persistence;

namespace RagHub.Infrastructure.Retrieval;

public class RetrievalService(
    RagDbContext db,
    IEmbeddingProvider embedder,
    IReranker reranker,
    IGenerationProvider generator,
    IOptions<RagSettings> opts,
    ILogger<RetrievalService> logger) : IRetrievalService
{
    private const int RrfK = 60; // standard RRF constant

    public async Task<RetrievalResult> SearchAsync(string query, int? topN, CancellationToken ct = default)
    {
        var cfg     = await db.RetrievalConfigs.FindAsync([1], ct) ?? FallbackConfig(opts.Value.Retrieval);
        int finalN  = topN ?? cfg.FinalN;
        int candK   = cfg.CandidateK;

        logger.LogInformation(
            "Retrieval start — query={Query} candK={CandK} finalN={FinalN} hybrid={Hybrid} rerank={Rerank} multiQuery={MultiQuery}",
            query, candK, finalN, cfg.UseHybrid, cfg.UseReranker, cfg.UseMultiQuery);

        // 0 — optionally expand into multiple paraphrased queries (off by default)
        var queries  = new List<string> { query };
        string pipeline = cfg.UseHybrid ? "hybrid" : "dense";

        if (cfg.UseMultiQuery)
        {
            try
            {
                var sw0 = Stopwatch.StartNew();
                var variations = await GenerateQueryVariationsAsync(query, cfg.MultiQueryCount, ct);
                queries.AddRange(variations);
                pipeline = "multiquery+" + pipeline;
                logger.LogInformation("Multi-query: {N} variation(s) in {Ms}ms: {Vars}",
                    variations.Count, sw0.ElapsedMilliseconds, string.Join(" | ", variations));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Multi-query generation failed — continuing with the original query only");
            }
        }

        // 3a/3b — dense (+ sparse) retrieval per query variant
        var rankedLists = new List<List<(int chunkId, int rank)>>();
        var chunkById   = new Dictionary<int, Chunk>();

        foreach (var q in queries)
        {
            var sw = Stopwatch.StartNew();
            var queryVec = new Vector(await embedder.EmbedAsync(q, ct));
            var dense = await DenseAsync(queryVec, candK, ct);
            foreach (var (chunk, _) in dense)
                chunkById[chunk.Id] = chunk;
            rankedLists.Add(dense.Select(d => (d.chunk.Id, d.rank)).ToList());
            logger.LogInformation("Dense retrieval for \"{Q}\": {N} candidates in {Ms}ms", q, dense.Count, sw.ElapsedMilliseconds);

            if (cfg.UseHybrid)
            {
                sw.Restart();
                var sparse = await SparseAsync(q, candK, ct);
                rankedLists.Add(sparse);
                logger.LogInformation("Sparse retrieval for \"{Q}\": {N} candidates in {Ms}ms", q, sparse.Count, sw.ElapsedMilliseconds);
            }
        }

        // Load any chunks referenced only by sparse ranks (not already pulled in via dense .Include)
        var missingIds = rankedLists.SelectMany(l => l.Select(x => x.chunkId)).Distinct()
            .Where(id => !chunkById.ContainsKey(id)).ToList();
        if (missingIds.Count > 0)
        {
            var extra = await db.Chunks.Include(c => c.Document)
                .Where(c => missingIds.Contains(c.Id)).ToListAsync(ct);
            foreach (var c in extra)
                chunkById[c.Id] = c;
        }

        var fused = FuseRRF(rankedLists, chunkById, candK);
        logger.LogInformation("RRF fusion across {Lists} ranked list(s): {N} candidates", rankedLists.Count, fused.Count);

        // Load document names for fused set
        var docIds   = fused.Select(f => f.chunk.DocumentId).Distinct().ToList();
        var docNames = await db.Documents
            .Where(d => docIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        List<RetrievedChunk> results;

        if (cfg.UseReranker && fused.Count > 0)
        {
            pipeline += "+rerank";
            try
            {
                var candidates = fused
                    .Select(f => (f.chunk.Id, f.chunk.Content))
                    .ToList();

                var sw = Stopwatch.StartNew();
                var reranked = await reranker.RerankAsync(query, candidates, ct);
                logger.LogInformation("Reranked: {N} results in {Ms}ms", reranked.Count, sw.ElapsedMilliseconds);

                var chunkMap = fused.ToDictionary(f => f.chunk.Id, f => f.chunk);
                results = reranked
                    .Take(finalN)
                    .Select(r => ToDto(chunkMap[r.ChunkId], r.Score, docNames))
                    .ToList();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Reranker call failed, falling back to RRF order");
                pipeline += "(reranker-unavailable)";
                results = fused.Take(finalN).Select(f => ToDto(f.chunk, f.score, docNames)).ToList();
            }
        }
        else
        {
            results = fused.Take(finalN).Select(f => ToDto(f.chunk, f.score, docNames)).ToList();
        }

        logger.LogInformation("Retrieval done — pipeline={Pipeline} returning {N} chunks: {Chunks}",
            pipeline, results.Count,
            string.Join(", ", results.Select(r => $"chunk:{r.ChunkId}({r.DocumentName})")));

        return new RetrievalResult(results, pipeline);
    }

    // --- multi-query: paraphrase the question to widen recall ---

    private async Task<List<string>> GenerateQueryVariationsAsync(string query, int count, CancellationToken ct)
    {
        string prompt = $"""
            Generate {count} alternative ways to ask the following question, using different words or
            sentence structure while preserving the exact same meaning. Output exactly {count} lines,
            one rephrased question per line, with no numbering, bullets, or extra commentary.

            Question: {query}
            """;

        var sb = new StringBuilder();
        await foreach (var token in generator.GenerateStreamAsync(prompt, ct))
            sb.Append(token);

        return sb.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Length > 0)
            .Take(count)
            .ToList();
    }

    // --- 3a: dense cosine search via pgvector <=> ---

    private async Task<List<(Chunk chunk, int rank)>> DenseAsync(Vector queryVec, int k, CancellationToken ct)
    {
        var chunks = await db.Chunks
            .Include(c => c.Document)
            .Where(c => c.Embedding != null)
            .OrderBy(c => c.Embedding!.CosineDistance(queryVec))
            .Take(k)
            .ToListAsync(ct);

        return chunks.Select((c, i) => (c, i + 1)).ToList();
    }

    // --- 3b: sparse full-text search via tsvector/GIN ---

    private async Task<List<(int chunkId, int rank)>> SparseAsync(string queryText, int k, CancellationToken ct)
    {
        // Use raw SQL for the tsvector shadow property — simpler and explicit.
        var rows = await db.Database
            .SqlQuery<SparseRow>($"""
                SELECT id AS chunk_id,
                       CAST(ROW_NUMBER() OVER (ORDER BY ts_rank(content_tsv, plainto_tsquery('simple', {queryText})) DESC) AS int) AS rank
                FROM chunks
                WHERE content_tsv @@ plainto_tsquery('simple', {queryText})
                  AND embedding IS NOT NULL
                LIMIT {k}
                """)
            .ToListAsync(ct);

        return rows.Select(r => (r.ChunkId, r.Rank)).ToList();
    }

    // --- RRF fusion — generalized over any number of ranked lists (hybrid sources × query variants) ---

    private List<(Chunk chunk, double score)> FuseRRF(
        List<List<(int chunkId, int rank)>> rankedLists,
        Dictionary<int, Chunk> chunkById,
        int k)
    {
        var scores = new Dictionary<int, double>();

        foreach (var list in rankedLists)
            foreach (var (chunkId, rank) in list)
                scores[chunkId] = scores.GetValueOrDefault(chunkId) + 1.0 / (RrfK + rank);

        return scores
            .Where(kv => chunkById.ContainsKey(kv.Key))
            .OrderByDescending(kv => kv.Value)
            .Take(k)
            .Select(kv => (chunkById[kv.Key], kv.Value))
            .ToList();
    }

    // --- helpers ---

    // Used only if the seeded RetrievalConfig row is somehow missing.
    private static RetrievalConfig FallbackConfig(RetrievalSettings s) => new()
    {
        CandidateK      = s.CandidateK,
        FinalN          = s.FinalN,
        UseHybrid       = s.UseHybrid,
        UseReranker     = s.UseReranker,
        UseMultiQuery   = s.UseMultiQuery,
        MultiQueryCount = s.MultiQueryCount,
    };

    private static RetrievedChunk ToDto(Chunk c, double score, Dictionary<int, string> docNames) =>
        new(c.Id, c.DocumentId,
            docNames.GetValueOrDefault(c.DocumentId, "unknown"),
            c.Content, c.HeadingPath, c.PageNo,
            c.CharStart, c.CharEnd, score);

    // Property names match snake_case SQL aliases (chunk_id → ChunkId, rank → Rank)
    private sealed class SparseRow
    {
        public int ChunkId { get; set; }
        public int Rank { get; set; }
    }
}
