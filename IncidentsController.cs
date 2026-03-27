using Dapper;
using ITSMPro.Data;
using ITSMPro.Models;
using ITSMPro.Services;
using Microsoft.AspNetCore.Mvc;

namespace ITSMPro.Controllers;

[ApiController]
[Route("api/incidents")]
public class IncidentsController : ControllerBase
{
    private readonly Database     _db;
    private readonly AuditService _audit;

    private static readonly Dictionary<string, string> SlaMap = new()
    {
        ["P1"] = "15 min",
        ["P2"] = "1h",
        ["P3"] = "4h",
        ["P4"] = "8h"
    };

    public IncidentsController(Database db, AuditService audit)
    {
        _db    = db;
        _audit = audit;
    }

    // GET /api/incidents?prio=P1&status=Offen&q=oauth
    [HttpGet]
    public IActionResult GetAll(
        [FromQuery] string? prio,
        [FromQuery] string? status,
        [FromQuery] string? q)
    {
        using var conn = _db.Open();
        var sql    = "SELECT * FROM incidents WHERE 1=1";
        var @params = new DynamicParameters();

        if (!string.IsNullOrEmpty(prio))
        {
            sql += " AND prio = @prio";
            @params.Add("prio", prio);
        }
        if (!string.IsNullOrEmpty(status))
        {
            sql += " AND status = @status";
            @params.Add("status", status);
        }
        if (!string.IsNullOrEmpty(q))
        {
            sql += " AND (LOWER(title) LIKE @q OR LOWER(id) LIKE @q)";
            @params.Add("q", $"%{q.ToLower()}%");
        }
        sql += " ORDER BY created_at DESC";

        var result = conn.Query<Incident>(sql, @params);
        return Ok(result);
    }

    // GET /api/incidents/{id}
    [HttpGet("{id}")]
    public IActionResult GetById(string id)
    {
        using var conn = _db.Open();
        var inc = conn.QuerySingleOrDefault<Incident>(
            "SELECT * FROM incidents WHERE id = @id", new { id });

        return inc is null ? NotFound(new { error = "Nicht gefunden" }) : Ok(inc);
    }

    // POST /api/incidents
    [HttpPost]
    public IActionResult Create([FromBody] IncidentCreateDto dto)
    {
        var required = new[] { dto.Id, dto.Title, dto.Prio, dto.Service, dto.Owner };
        if (required.Any(string.IsNullOrWhiteSpace))
            return BadRequest(new { error = "Pflichtfeld fehlt: id, title, prio, service, owner" });

        var now = DateTime.UtcNow.ToString("o");
        var sla = SlaMap.GetValueOrDefault(dto.Prio, "—");

        try
        {
            using var conn = _db.Open();
            conn.Execute("""
                INSERT INTO incidents
                    (id, title, prio, status, service, owner, channel, release_id, description, sla, created_at, updated_at)
                VALUES
                    (@Id, @Title, @Prio, @Status, @Service, @Owner, @Channel, @ReleaseId, @Description, @Sla, @Now, @Now)
                """,
                new
                {
                    dto.Id, dto.Title, dto.Prio,
                    Status      = dto.Status ?? "Offen",
                    dto.Service, dto.Owner,
                    Channel     = dto.Channel ?? "",
                    ReleaseId   = dto.ReleaseId ?? "",
                    Description = dto.Description ?? "",
                    Sla         = sla,
                    Now         = now
                });

            _audit.Log("incident", dto.Id, "CREATE",
                $"Prio={dto.Prio} Service={dto.Service}");

            return Created($"/api/incidents/{dto.Id}", new { ok = true, id = dto.Id });
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex)
            when (ex.SqliteErrorCode == 19) // UNIQUE constraint
        {
            return Conflict(new { error = "ID bereits vorhanden" });
        }
    }

    // PUT /api/incidents/{id}
    [HttpPut("{id}")]
    public IActionResult Update(string id, [FromBody] IncidentUpdateDto dto)
    {
        var now = DateTime.UtcNow.ToString("o");
        using var conn = _db.Open();

        conn.Execute("""
            UPDATE incidents
            SET    status      = COALESCE(@Status, status),
                   owner       = COALESCE(@Owner, owner),
                   description = COALESCE(@Description, description),
                   updated_at  = @Now
            WHERE  id = @Id
            """,
            new { dto.Status, dto.Owner, dto.Description, Now = now, Id = id });

        _audit.Log("incident", id, "UPDATE",
            System.Text.Json.JsonSerializer.Serialize(
                new { dto.Status, dto.Owner }));

        return Ok(new { ok = true });
    }
}
