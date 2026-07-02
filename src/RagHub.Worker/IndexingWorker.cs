using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pgvector;
using RagHub.Core.Domain;
using RagHub.Core.Interfaces;
using RagHub.Infrastructure.Chunking;
using RagHub.Infrastructure.Persistence;

namespace RagHub.Worker;

public class IndexingWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<IndexingWorker> logger) : BackgroundService
{
    private const int EmbedBatchSize = 32;
    // Vector column was created with this dimension — mismatch must fail loudly.
    private const int ExpectedDimension = 1024;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("IndexingWorker started");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // A transient failure (e.g. DB briefly unreachable) must not permanently kill the polling loop.
                logger.LogError(ex, "Indexing poll cycle failed — will retry next tick");
            }
        }
    }

    private async Task ProcessPendingAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db        = scope.ServiceProvider.GetRequiredService<RagDbContext>();
        var parsers   = scope.ServiceProvider.GetServices<IDocumentParser>().ToList();
        var chunker   = scope.ServiceProvider.GetRequiredService<IChunker>();
        var embedder  = scope.ServiceProvider.GetRequiredService<IEmbeddingProvider>();

        var pending = await db.Documents
            .Where(d => d.Status == DocumentStatus.Pending)
            .ToListAsync(ct);

        foreach (var doc in pending)
            await IndexDocumentAsync(doc, db, parsers, chunker, embedder, ct);
    }

    private async Task IndexDocumentAsync(
        Document doc,
        RagDbContext db,
        List<IDocumentParser> parsers,
        IChunker chunker,
        IEmbeddingProvider embedder,
        CancellationToken ct)
    {
        logger.LogInformation("Indexing document {Id} ({Name})", doc.Id, doc.Name);
        doc.Status = DocumentStatus.Processing;
        await db.SaveChangesAsync(ct);

        try
        {
            if (string.IsNullOrEmpty(doc.FilePath) || !File.Exists(doc.FilePath))
                throw new InvalidOperationException($"File not found: {doc.FilePath}");

            string ext    = Path.GetExtension(doc.FilePath);
            var parser    = parsers.FirstOrDefault(p => p.CanParse(ext))
                ?? throw new NotSupportedException($"No parser for extension '{ext}'");

            var swStep = System.Diagnostics.Stopwatch.StartNew();
            var parsed    = await parser.ParseAsync(doc.FilePath, ct);
            logger.LogInformation("Parsed {Ext} in {Ms}ms → {Chars} chars, {Pages} pages",
                ext, swStep.ElapsedMilliseconds, parsed.FullText.Length, parsed.Pages.Count);

            string docType = string.IsNullOrWhiteSpace(doc.Type)
                ? StructuralChunker.DetectDocType(parsed.FullText)
                : doc.Type;
            logger.LogInformation("Doc type: {DocType} (stored type was '{Stored}')", docType, doc.Type);

            var profile = doc.ChunkingProfileId is int profileId
                ? await db.ChunkingProfiles.FindAsync([profileId], ct)
                    ?? throw new InvalidOperationException($"Chunking profile {profileId} not found.")
                : await db.ChunkingProfiles.FindAsync([1], ct)
                    ?? new ChunkingProfile { Name = "Default", Strategy = "auto", MaxChunkSize = 1500, Overlap = 100 };
            logger.LogInformation("Chunking profile: {Name} (maxChunkSize={Max}, overlap={Overlap}, strategy={Strategy})",
                profile.Name, profile.MaxChunkSize, profile.Overlap, profile.Strategy);

            swStep.Restart();
            var chunkResults = chunker.Chunk(parsed, docType, profile);
            logger.LogInformation("Chunked into {Count} chunks in {Ms}ms", chunkResults.Count, swStep.ElapsedMilliseconds);

            if (chunkResults.Count == 0)
                throw new InvalidOperationException(
                    "No text could be extracted from this file. " +
                    "If this is a scanned/image PDF, OCR support is required.");

            // Guard: embedding dimension must match vector(N) column
            if (embedder.Dimensions != ExpectedDimension)
                throw new InvalidOperationException(
                    $"Embedding provider '{embedder.ModelName}' returns {embedder.Dimensions}-dim vectors " +
                    $"but the database column is vector({ExpectedDimension}). " +
                    $"Re-create the schema or switch to a {ExpectedDimension}-dim model.");

            // Remove any stale chunks from a previous attempt
            db.Chunks.RemoveRange(db.Chunks.Where(c => c.DocumentId == doc.Id));

            var chunks = chunkResults.Select(cr => new Chunk
            {
                DocumentId  = doc.Id,
                ChunkIndex  = cr.ChunkIndex,
                Content     = cr.Content,
                HeadingPath = cr.HeadingPath,
                PageNo      = cr.PageNo,
                CharStart   = cr.CharStart,
                CharEnd     = cr.CharEnd,
                TokenCount  = cr.TokenCount,
                EmbeddingModel = embedder.ModelName,
                EmbeddingDim   = embedder.Dimensions,
            }).ToList();

            db.Chunks.AddRange(chunks);
            await db.SaveChangesAsync(ct);

            int totalBatches = (int)Math.Ceiling(chunks.Count / (double)EmbedBatchSize);
            logger.LogInformation("Embedding {Count} chunks in {Batches} batches of {Size} via {Model}",
                chunks.Count, totalBatches, EmbedBatchSize, embedder.ModelName);

            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < chunks.Count; i += EmbedBatchSize)
            {
                var batch = chunks.Skip(i).Take(EmbedBatchSize).ToList();
                var texts = batch.Select(c => c.Content).ToList();
                swStep.Restart();
                var vectors = await embedder.EmbedBatchAsync(texts, ct);

                for (int j = 0; j < batch.Count; j++)
                    batch[j].Embedding = new Vector(vectors[j]);

                await db.SaveChangesAsync(ct);
                int batchNum = i / EmbedBatchSize + 1;
                logger.LogInformation("  Batch {Batch}/{Total} done in {Ms}ms (chunks {From}–{To})",
                    batchNum, totalBatches, swStep.ElapsedMilliseconds, i, i + batch.Count - 1);
            }
            logger.LogInformation("All embeddings done in {Ms}ms total", swTotal.ElapsedMilliseconds);

            doc.Status       = DocumentStatus.Indexed;
            doc.ChunkCount   = chunks.Count;
            doc.Type         = docType;
            doc.EmbeddingModel = embedder.ModelName;
            doc.EmbeddingDim   = embedder.Dimensions;
            doc.ChunkingProfileId = profile.Id == 0 ? null : profile.Id;
            doc.ChunkSizeUsed  = profile.MaxChunkSize;
            doc.OverlapUsed    = profile.Overlap;
            doc.ErrorMessage = null;

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Document {Id} indexed: {Count} chunks, model={Model}, dim={Dim}",
                doc.Id, chunks.Count, embedder.ModelName, embedder.Dimensions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to index document {Id}", doc.Id);
            doc.Status       = DocumentStatus.Failed;
            doc.ErrorMessage = ex.Message;
            await db.SaveChangesAsync(ct);
        }
    }
}
