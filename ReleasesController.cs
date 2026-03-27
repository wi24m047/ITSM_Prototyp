using Dapper;
using ITSMPro.Data;
using ITSMPro.Models;
using ITSMPro.Services;
using Microsoft.AspNetCore.Mvc;

namespace ITSMPro.Controllers;

[ApiController]
[Route("api/releases")]
public class ReleasesController : ControllerBase
{
    private readonly Database     _db;
    private readonly AuditService _audit;

    public ReleasesController(Database db, AuditService audit)
    {
        _db    = db;
        _audit = audit;
    }

    // GET /api/releases
    [HttpGet]
    public IActionResult GetAll()
    {
        using var conn = _db.Open();
        return Ok(conn.Query<Release>(
            "SELECT * FROM releases ORDER BY created_at DESC"));
    }

    // GET /api/releases/upcoming?days=2
    [HttpGet("upcoming")]
    public IActionResult Upcoming([FromQuery] int days = 2)
    {
        using var conn = _db.Open();
        var cutoff = DateTime.Today.AddDays(days).ToString("dd.MM.yyyy");
        var today  = DateTime.Today.ToString("dd.MM.yyyy");
        // Compare as text dd.MM.yyyy — fetch all non-completed and filter in C#
        var all = conn.Query<Release>(
            "SELECT * FROM releases WHERE status != 'Abgeschlossen' ORDER BY date_prod");
        var result = all.Where(r =>
        {
            if (string.IsNullOrEmpty(r.DateProd)) return false;
            if (DateTime.TryParseExact(r.DateProd, "dd.MM.yyyy",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var d))
                return d >= DateTime.Today && d <= DateTime.Today.AddDays(days);
            return false;
        });
        return Ok(result);
    }

    // GET /api/releases/{id}
    [HttpGet("{id}")]
    public IActionResult GetById(string id)
    {
        using var conn = _db.Open();
        var rel = conn.QuerySingleOrDefault<Release>(
            "SELECT * FROM releases WHERE id = @id", new { id });
        return rel is null ? NotFound(new { error = "Nicht gefunden" }) : Ok(rel);
    }

    // GET /api/releases/{id}/history  — audit trail for this release
    [HttpGet("{id}/history")]
    public IActionResult GetHistory(string id)
    {
        return Ok(_audit.GetForEntity(id));
    }

    // POST /api/releases
    [HttpPost]
    public IActionResult Create([FromBody] ReleaseCreateDto dto)
    {
        var required = new[] { dto.Id, dto.Name, dto.Typ, dto.Risiko, dto.Notes, dto.Services };
        if (required.Any(string.IsNullOrWhiteSpace))
            return BadRequest(new { error = "Pflichtfeld fehlt: id, name, typ, risiko, notes, services" });
        if (string.IsNullOrWhiteSpace(dto.DateAbnahme))
            return BadRequest(new { error = "Abnahme-Datum ist Pflichtfeld" });
        if (string.IsNullOrWhiteSpace(dto.DateProd))
            return BadRequest(new { error = "Prod-Datum ist Pflichtfeld" });

        var now = DateTime.UtcNow.ToString("o");

        try
        {
            using var conn = _db.Open();
            conn.Execute("""
                INSERT INTO releases
                    (id, name, typ, risiko, status, environment, notes, services,
                     rollback, test_evidence, known_risks,
                     approver, approve_date, approve_reason,
                     date_test, date_abnahme, date_prod,
                     created_at, updated_at)
                VALUES
                    (@Id, @Name, @Typ, @Risiko, @Status, @Env, @Notes, @Services,
                     @Rollback, @TestEvidence, @KnownRisks,
                     '', '', '',
                     @DateTest, @DateAbnahme, @DateProd,
                     @Now, @Now)
                """,
                new
                {
                    dto.Id, dto.Name, dto.Typ, dto.Risiko,
                    Status       = dto.Status ?? "Offen",
                    Env          = dto.Environment ?? "Dev",
                    dto.Notes, dto.Services,
                    Rollback     = dto.Rollback     ?? "",
                    TestEvidence = dto.TestEvidence ?? "",
                    KnownRisks   = dto.KnownRisks   ?? "",
                    DateTest     = dto.DateTest     ?? "",
                    DateAbnahme  = dto.DateAbnahme  ?? "",
                    DateProd     = dto.DateProd     ?? "",
                    Now          = now
                });

            _audit.Log("release", dto.Id, "CREATE",
                $"Typ={dto.Typ} Risiko={dto.Risiko} Prod={dto.DateProd}");

            return Created($"/api/releases/{dto.Id}", new { ok = true, id = dto.Id });
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex)
            when (ex.SqliteErrorCode == 19)
        {
            return Conflict(new { error = "ID bereits vorhanden" });
        }
    }

    // POST /api/releases/{id}/approve
    [HttpPost("{id}/approve")]
    public IActionResult Approve(string id, [FromBody] ReleaseApproveDto dto)
    {
        var now = DateTime.UtcNow.ToString("o");
        var ts  = DateTime.UtcNow.ToString("dd.MM.yyyy HH:mm");

        using var conn = _db.Open();
        conn.Execute("""
            UPDATE releases
            SET    approver       = @Approver,
                   approve_date   = @ApproveDate,
                   approve_reason = @Reason,
                   status         = @Status,
                   updated_at     = @Now
            WHERE  id = @Id
            """,
            new
            {
                Approver    = dto.Approver ?? "Anna Müller",
                ApproveDate = ts,
                Reason      = dto.Reason   ?? "Freigabe erteilt",
                Status      = dto.Status   ?? "Freigegeben",
                Now         = now,
                Id          = id
            });

        _audit.Log("release", id, "APPROVE",
            $"Approver={dto.Approver} Reason={dto.Reason}");

        return Ok(new { ok = true });
    }
}
