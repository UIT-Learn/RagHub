using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RagHub.API.Auth;
using RagHub.API.DTOs;
using RagHub.Core.Domain;
using RagHub.Core.Settings;
using RagHub.Infrastructure.Persistence;

namespace RagHub.API.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsController(RagDbContext db, IOptions<RagSettings> opts) : ControllerBase
{
    private static readonly HashSet<string> _allowed =
        new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".docx", ".txt", ".md" };

    [HttpPost]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] string category,
        [FromForm] string? docType,
        [FromForm] int? chunkingProfileId,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        string ext = Path.GetExtension(file.FileName);
        if (!_allowed.Contains(ext))
            return BadRequest(new { error = $"Unsupported file type '{ext}'. Allowed: pdf, docx, txt, md." });

        if (chunkingProfileId is int pid && !await db.ChunkingProfiles.AnyAsync(p => p.Id == pid, ct))
            return BadRequest(new { error = $"Chunking profile {pid} not found." });

        string uploadDir = Path.GetFullPath(opts.Value.Storage.UploadPath);
        Directory.CreateDirectory(uploadDir);

        string savedName = $"{Guid.NewGuid()}{ext}";
        string filePath  = Path.Combine(uploadDir, savedName);

        await using (var stream = System.IO.File.Create(filePath))
            await file.CopyToAsync(stream, ct);

        var doc = new Document
        {
            Name              = file.FileName,
            Category          = category,
            Type              = docType ?? string.Empty,
            FilePath          = filePath,
            Status            = DocumentStatus.Pending,
            ChunkingProfileId = chunkingProfileId,
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = doc.Id }, DocumentMapper.ToResponse(doc));
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var docs = await db.Documents
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => DocumentMapper.ToResponse(d))
            .ToListAsync(ct);
        return Ok(docs);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var doc = await db.Documents.FindAsync([id], ct);
        return doc is null ? NotFound() : Ok(DocumentMapper.ToResponse(doc));
    }

    [HttpGet("{id:int}/chunks")]
    public async Task<IActionResult> GetChunks(int id, CancellationToken ct)
    {
        bool exists = await db.Documents.AnyAsync(d => d.Id == id, ct);
        if (!exists) return NotFound();

        var chunks = await db.Chunks
            .Where(c => c.DocumentId == id)
            .OrderBy(c => c.ChunkIndex)
            .Select(c => DocumentMapper.ToResponse(c))
            .ToListAsync(ct);

        return Ok(chunks);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var doc = await db.Documents.FindAsync([id], ct);
        if (doc is null) return NotFound();

        if (doc.Status == DocumentStatus.Processing)
            return Conflict(new { error = "Cannot delete a document that is currently being processed." });

        db.Chunks.RemoveRange(db.Chunks.Where(c => c.DocumentId == id));
        db.Documents.Remove(doc);
        await db.SaveChangesAsync(ct);

        if (System.IO.File.Exists(doc.FilePath))
            System.IO.File.Delete(doc.FilePath);

        return NoContent();
    }

    // Portable export for edge/offline use: chunks + their embeddings + the exact
    // embedding model name, so a downstream device can load vectors directly instead
    // of re-embedding (and never mixes vectors from a different model in).
    [HttpGet("{id:int}/export")]
    [ApiKey]
    public async Task<IActionResult> Export(int id, CancellationToken ct)
    {
        var doc = await db.Documents.FindAsync([id], ct);
        if (doc is null) return NotFound();
        if (doc.Status != DocumentStatus.Indexed)
            return Conflict(new { error = $"Document is '{doc.Status}', not Indexed — nothing to export yet." });

        var chunks = await db.Chunks
            .Where(c => c.DocumentId == id)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync(ct);

        var bundle = DocumentMapper.ToExportBundle(doc, chunks);
        string fileName = $"{Path.GetFileNameWithoutExtension(doc.Name)}_export.json";
        return File(
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(bundle,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }),
            "application/json", fileName);
    }

    [HttpPost("{id:int}/reindex")]
    public async Task<IActionResult> Reindex(int id, [FromBody] ReindexRequest? request, CancellationToken ct)
    {
        var doc = await db.Documents.FindAsync([id], ct);
        if (doc is null) return NotFound();

        if (doc.Status == DocumentStatus.Processing)
            return Conflict(new { error = "Document is currently being processed." });

        if (request?.ChunkingProfileId is int pid)
        {
            if (!await db.ChunkingProfiles.AnyAsync(p => p.Id == pid, ct))
                return BadRequest(new { error = $"Chunking profile {pid} not found." });
            doc.ChunkingProfileId = pid;
        }

        doc.Status       = DocumentStatus.Pending;
        doc.ErrorMessage = null;
        await db.SaveChangesAsync(ct);

        return Accepted(DocumentMapper.ToResponse(doc));
    }
}
