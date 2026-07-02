using Microsoft.AspNetCore.Mvc;
using RagHub.API.DTOs;
using RagHub.Core.Domain;
using RagHub.Infrastructure.Persistence;

namespace RagHub.API.Controllers;

[ApiController]
[Route("api/settings/retrieval")]
public class SettingsController(RagDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var cfg = await db.RetrievalConfigs.FindAsync([1], ct);
        if (cfg is null) return NotFound(new { error = "Retrieval config row missing — re-run migrations." });
        return Ok(SettingsMapper.ToResponse(cfg));
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] RetrievalConfigRequest request, CancellationToken ct)
    {
        if (request.CandidateK <= 0 || request.FinalN <= 0 || request.FinalN > request.CandidateK)
            return BadRequest(new { error = "CandidateK must be positive and FinalN must be <= CandidateK." });

        var cfg = await db.RetrievalConfigs.FindAsync([1], ct);
        if (cfg is null)
        {
            cfg = new RetrievalConfig { Id = 1 };
            db.RetrievalConfigs.Add(cfg);
        }

        cfg.CandidateK      = request.CandidateK;
        cfg.FinalN          = request.FinalN;
        cfg.UseHybrid       = request.UseHybrid;
        cfg.UseReranker     = request.UseReranker;
        cfg.UseMultiQuery   = request.UseMultiQuery;
        cfg.MultiQueryCount = request.MultiQueryCount;
        cfg.UpdatedAt       = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(SettingsMapper.ToResponse(cfg));
    }
}
