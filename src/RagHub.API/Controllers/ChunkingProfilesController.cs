using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RagHub.API.DTOs;
using RagHub.Core.Domain;
using RagHub.Infrastructure.Persistence;

namespace RagHub.API.Controllers;

[ApiController]
[Route("api/chunking-profiles")]
public class ChunkingProfilesController(RagDbContext db) : ControllerBase
{
    private static readonly HashSet<string> _strategies =
        new(StringComparer.OrdinalIgnoreCase) { "auto", "policy", "technical", "api", "legal", "faq", "fixed" };

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await db.ChunkingProfiles.OrderBy(p => p.Id).ToListAsync(ct);
        return Ok(items.Select(SettingsMapper.ToResponse));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ChunkingProfileRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name must not be empty." });
        if (!_strategies.Contains(request.Strategy))
            return BadRequest(new { error = $"Strategy must be one of: {string.Join(", ", _strategies)}." });
        if (request.MaxChunkSize <= 0 || request.Overlap < 0 || request.Overlap >= request.MaxChunkSize)
            return BadRequest(new { error = "MaxChunkSize must be positive and Overlap must be smaller than it." });

        var profile = new ChunkingProfile
        {
            Name         = request.Name,
            Strategy     = request.Strategy,
            MaxChunkSize = request.MaxChunkSize,
            Overlap      = request.Overlap,
        };
        db.ChunkingProfiles.Add(profile);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(List), SettingsMapper.ToResponse(profile));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ChunkingProfileRequest request, CancellationToken ct)
    {
        var profile = await db.ChunkingProfiles.FindAsync([id], ct);
        if (profile is null) return NotFound();
        if (!_strategies.Contains(request.Strategy))
            return BadRequest(new { error = $"Strategy must be one of: {string.Join(", ", _strategies)}." });
        if (request.MaxChunkSize <= 0 || request.Overlap < 0 || request.Overlap >= request.MaxChunkSize)
            return BadRequest(new { error = "MaxChunkSize must be positive and Overlap must be smaller than it." });

        profile.Name         = request.Name;
        profile.Strategy     = request.Strategy;
        profile.MaxChunkSize = request.MaxChunkSize;
        profile.Overlap      = request.Overlap;
        await db.SaveChangesAsync(ct);

        return Ok(SettingsMapper.ToResponse(profile));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var profile = await db.ChunkingProfiles.FindAsync([id], ct);
        if (profile is null) return NotFound();

        bool inUse = await db.Documents.AnyAsync(d => d.ChunkingProfileId == id, ct);
        if (inUse)
            return Conflict(new { error = "Profile is in use by one or more documents." });

        db.ChunkingProfiles.Remove(profile);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
