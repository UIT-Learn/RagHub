using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using RagHub.Core.Domain;

namespace RagHub.Infrastructure.Persistence;

public class RagDbContext(DbContextOptions<RagDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Chunk> Chunks => Set<Chunk>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();
    public DbSet<ChunkingProfile> ChunkingProfiles => Set<ChunkingProfile>();
    public DbSet<RetrievalConfig> RetrievalConfigs => Set<RetrievalConfig>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasPostgresExtension("vector");

        model.Entity<Document>(e =>
        {
            e.Property(d => d.Status).HasConversion<string>();
            e.HasOne(d => d.ChunkingProfile)
             .WithMany()
             .HasForeignKey(d => d.ChunkingProfileId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        model.Entity<ChunkingProfile>(e =>
        {
            var seededAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            e.HasData(
                new ChunkingProfile { Id = 1, Name = "Default", Strategy = "auto", MaxChunkSize = 1500, Overlap = 100, CreatedAt = seededAt },
                new ChunkingProfile { Id = 2, Name = "Legal / Contract", Strategy = "legal", MaxChunkSize = 1200, Overlap = 150, CreatedAt = seededAt },
                new ChunkingProfile { Id = 3, Name = "FAQ", Strategy = "faq", MaxChunkSize = 800, Overlap = 50, CreatedAt = seededAt },
                new ChunkingProfile { Id = 4, Name = "Generic Fixed-size", Strategy = "fixed", MaxChunkSize = 1000, Overlap = 150, CreatedAt = seededAt });
        });

        model.Entity<RetrievalConfig>(e =>
        {
            e.HasData(new RetrievalConfig
            {
                Id = 1,
                CandidateK = 20,
                FinalN = 5,
                UseHybrid = true,
                UseReranker = true,
                UseMultiQuery = false,
                MultiQueryCount = 3,
                UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            });
        });

        model.Entity<Chunk>(e =>
        {
            e.HasOne(c => c.Document)
             .WithMany(d => d.Chunks)
             .HasForeignKey(c => c.DocumentId)
             .OnDelete(DeleteBehavior.Cascade);

            e.Property(c => c.Embedding).HasColumnType("vector(1024)");

            // generated tsvector for sparse retrieval (Phase 3b)
            e.Property<NpgsqlTypes.NpgsqlTsVector>("ContentTsv")
             .HasComputedColumnSql("to_tsvector('english', content)", stored: true);
            e.HasIndex("ContentTsv")
             .HasMethod("GIN")
             .HasDatabaseName("ix_chunks_content_tsv");

            e.HasIndex(c => c.DocumentId);
        });

        model.Entity<Feedback>(e =>
        {
            e.Property(f => f.RetrievedChunkIds).HasColumnType("jsonb");
        });

        model.Entity<Evaluation>(e =>
        {
            e.Property(ev => ev.ExpectedSources).HasColumnType("jsonb");
            e.Property(ev => ev.ActualChunkIds).HasColumnType("jsonb");
        });
    }
}
