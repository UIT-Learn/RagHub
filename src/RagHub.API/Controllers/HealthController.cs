using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RagHub.Infrastructure.Persistence;

namespace RagHub.API.Controllers;

[ApiController]
[Route("api")]
public class HealthController(RagDbContext db) : ControllerBase
{
    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
            return Ok(new { status = "healthy", database = "connected" });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { status = "unhealthy", database = ex.Message });
        }
    }
}
