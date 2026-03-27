using Dapper;
using ITSMPro.Data;
using ITSMPro.Models;
using ITSMPro.Services;
using Microsoft.AspNetCore.Mvc;

namespace ITSMPro.Controllers;

[ApiController]
[Route("api/problems")]
public class ProblemsController : ControllerBase
{
    private readonly Database     _db;
    private readonly AuditService _audit;

    public ProblemsController(Database db, AuditService audit)
    {
        _db    = db;
        _audit = audit;
    }

    // GET /api/problems
    [HttpGet]
    public IActionResult GetAll()
    {
        using var conn = _db.Open();
        return Ok(conn.Query<Problem>(
            "SELECT * FROM problems ORDER BY created_at DESC"));
    }

    // POST /api/problems
    [HttpPost]
    public IActionResult Create([FromBody] ProblemCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.IncidentId) ||
            string.IsNullOrWhiteSpace(dto.RootCause))
            return BadRequest(new { error = "incident_id und root_cause sind Pflicht" });

        var now = DateTime.UtcNow.ToString("o");
        var id  = $"PROB-{DateTime.UtcNow:yyyyMMddHHmmss}";

        using var conn = _db.Open();
        conn.Execute("""
            INSERT INTO problems
                (id, incident_id, problem_type, root_cause, method,
                 why1, why2, why3, lessons, status, created_at, updated_at)
            VALUES
                (@Id, @IncidentId, @ProblemType, @RootCause, @Method,
                 @Why1, @Why2, @Why3, @Lessons, 'Offen', @Now, @Now)
            """,
            new
            {
                Id          = id,
                IncidentId  = dto.IncidentId,
                ProblemType = dto.ProblemType ?? "Normal",
                RootCause   = dto.RootCause,
                Method      = dto.Method      ?? "5why",
                Why1        = dto.Why1        ?? "",
                Why2        = dto.Why2        ?? "",
                Why3        = dto.Why3        ?? "",
                Lessons     = dto.Lessons     ?? "",
                Now         = now
            });

        _audit.Log("problem", id, "CREATE",
            $"Method={dto.Method} Incident={dto.IncidentId}");

        return Created($"/api/problems/{id}", new { ok = true, id });
    }
}
