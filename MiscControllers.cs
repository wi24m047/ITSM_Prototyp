using Dapper;
using ITSMPro.Data;
using ITSMPro.Models;
using ITSMPro.Services;
using Microsoft.AspNetCore.Mvc;

namespace ITSMPro.Controllers;

// ── Audit ─────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/audit")]
public class AuditController : ControllerBase
{
    private readonly AuditService _audit;
    public AuditController(AuditService audit) => _audit = audit;

    // GET /api/audit?entity_id=INC-0041
    [HttpGet]
    public IActionResult Get([FromQuery] string? entity_id)
    {
        return Ok(string.IsNullOrEmpty(entity_id)
            ? _audit.GetAll()
            : _audit.GetForEntity(entity_id));
    }
}

// ── KPIs ──────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/kpis")]
public class KpisController : ControllerBase
{
    private readonly Database _db;
    public KpisController(Database db) => _db = db;

    [HttpGet]
    public IActionResult Get()
    {
        using var conn = _db.Open();

        var totalInc  = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM incidents");
        var openP1    = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM incidents WHERE prio='P1' AND status!='Gelöst'");
        var openP2    = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM incidents WHERE prio='P2' AND status!='Gelöst'");
        var openP3    = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM incidents WHERE prio='P3' AND status!='Gelöst'");
        var solved    = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM incidents WHERE status='Gelöst'");
        var relTotal  = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM releases");
        var relOk     = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM releases WHERE status='Abgeschlossen'");

        return Ok(new KpiResult
        {
            OpenP1             = openP1,
            OpenP2             = openP2,
            OpenP3             = openP3,
            TotalIncidents     = totalInc,
            Solved             = solved,
            ReleaseSuccessRate = relTotal > 0 ? (int)Math.Round((double)relOk / relTotal * 100) : 0
        });
    }
}

// ── Health ────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly Database _db;
    public HealthController(Database db) => _db = db;

    [HttpGet]
    public IActionResult Get()
    {
        using var conn = _db.Open();
        return Ok(new
        {
            status = "ok",
            runtime = "ASP.NET Core 9 + Dapper + SQLite",
            counts = new
            {
                incidents   = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM incidents"),
                releases    = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM releases"),
                problems    = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM problems"),
                log_entries = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM log_entries"),
                audit_log   = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM audit_log")
            }
        });
    }
}
