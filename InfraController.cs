using Dapper;
using ITSMPro.Data;
using ITSMPro.Models;
using ITSMPro.Services;
using Microsoft.AspNetCore.Mvc;

namespace ITSMPro.Controllers;

[ApiController]
[Route("api/infra")]
public class InfraController : ControllerBase
{
    private readonly Database     _db;
    private readonly AuditService _audit;

    public InfraController(Database db, AuditService audit)
    {
        _db    = db;
        _audit = audit;
    }

    // ── Items ─────────────────────────────────────────────────────────────────

    // GET /api/infra?typ=Service&status=Aktiv
    [HttpGet]
    public IActionResult GetAll([FromQuery] string? typ, [FromQuery] string? status)
    {
        using var conn = _db.Open();
        var sql    = "SELECT * FROM infra_items WHERE 1=1";
        var @params = new DynamicParameters();
        if (!string.IsNullOrEmpty(typ))    { sql += " AND typ    = @typ";    @params.Add("typ",    typ);    }
        if (!string.IsNullOrEmpty(status)) { sql += " AND status = @status"; @params.Add("status", status); }
        sql += " ORDER BY typ, name";
        return Ok(conn.Query<InfraItem>(sql, @params));
    }

    // GET /api/infra/{id}  — full detail with relations, incidents, releases
    [HttpGet("{id}")]
    public IActionResult GetDetail(string id)
    {
        using var conn = _db.Open();
        var item = conn.QuerySingleOrDefault<InfraItem>(
            "SELECT * FROM infra_items WHERE id = @id", new { id });
        if (item is null) return NotFound(new { error = "Nicht gefunden" });

        var relations = conn.Query<InfraRelation>("""
            SELECT r.*,
                   s.name AS source_name, s.typ AS source_typ,
                   t.name AS target_name, t.typ AS target_typ
            FROM   infra_relations r
            JOIN   infra_items s ON s.id = r.source_id
            JOIN   infra_items t ON t.id = r.target_id
            WHERE  r.source_id = @id OR r.target_id = @id
            """, new { id }).ToList();

        var incidents = conn.Query<Incident>("""
            SELECT i.* FROM incidents i
            JOIN   incident_infra ii ON ii.incident_id = i.id
            WHERE  ii.infra_id = @id AND i.status != 'Gelöst'
            ORDER  BY i.created_at DESC
            """, new { id }).ToList();

        var releases = conn.Query<Release>("""
            SELECT r.* FROM releases r
            JOIN   release_infra ri ON ri.release_id = r.id
            WHERE  ri.infra_id = @id
            ORDER  BY r.created_at DESC
            LIMIT  10
            """, new { id }).ToList();

        return Ok(new InfraDetail
        {
            Item      = item,
            Relations = relations,
            Incidents = incidents,
            Releases  = releases
        });
    }

    // POST /api/infra
    [HttpPost]
    public IActionResult Create([FromBody] InfraItemCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.Name)
            || string.IsNullOrWhiteSpace(dto.Typ))
            return BadRequest(new { error = "id, name, typ sind Pflicht" });

        var now = DateTime.UtcNow.ToString("o");
        try
        {
            using var conn = _db.Open();
            conn.Execute("""
                INSERT INTO infra_items
                    (id,name,typ,status,umgebung,owner,beschreibung,ip_adresse,version,log_pfad,created_at,updated_at)
                VALUES
                    (@Id,@Name,@Typ,@Status,@Umgebung,@Owner,@Beschreibung,@IpAdresse,@Version,@LogPfad,@Now,@Now)
                """,
                new {
                    dto.Id, dto.Name, dto.Typ,
                    Status      = dto.Status      ?? "Aktiv",
                    Umgebung    = dto.Umgebung    ?? "",
                    Owner       = dto.Owner       ?? "",
                    Beschreibung= dto.Beschreibung?? "",
                    IpAdresse   = dto.IpAdresse   ?? "",
                    Version     = dto.Version     ?? "",
                    LogPfad     = dto.LogPfad     ?? "",
                    Now         = now
                });
            _audit.Log("infra", dto.Id, "CREATE", $"Typ={dto.Typ} Status={dto.Status}");
            return Created($"/api/infra/{dto.Id}", new { ok = true, id = dto.Id });
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return Conflict(new { error = "ID bereits vorhanden" });
        }
    }

    // PUT /api/infra/{id}
    [HttpPut("{id}")]
    public IActionResult Update(string id, [FromBody] InfraItemUpdateDto dto)
    {
        var now = DateTime.UtcNow.ToString("o");
        using var conn = _db.Open();
        conn.Execute("""
            UPDATE infra_items SET
                status       = COALESCE(@Status,       status),
                owner        = COALESCE(@Owner,        owner),
                beschreibung = COALESCE(@Beschreibung, beschreibung),
                ip_adresse   = COALESCE(@IpAdresse,    ip_adresse),
                version      = COALESCE(@Version,      version),
                log_pfad     = COALESCE(@LogPfad,      log_pfad),
                updated_at   = @Now
            WHERE id = @Id
            """,
            new { dto.Status, dto.Owner, dto.Beschreibung, dto.IpAdresse, dto.Version, dto.LogPfad, Now = now, Id = id });
        _audit.Log("infra", id, "UPDATE", $"Status={dto.Status}");
        return Ok(new { ok = true });
    }

    // DELETE /api/infra/{id}
    [HttpDelete("{id}")]
    public IActionResult Delete(string id)
    {
        using var conn = _db.Open();
        conn.Execute("DELETE FROM infra_relations  WHERE source_id = @id OR target_id = @id", new { id });
        conn.Execute("DELETE FROM incident_infra   WHERE infra_id  = @id", new { id });
        conn.Execute("DELETE FROM release_infra    WHERE infra_id  = @id", new { id });
        conn.Execute("DELETE FROM infra_items      WHERE id        = @id", new { id });
        _audit.Log("infra", id, "DELETE");
        return Ok(new { ok = true });
    }

    // ── Relations between infra items ─────────────────────────────────────────

    // GET /api/infra/relations
    [HttpGet("relations")]
    public IActionResult GetRelations([FromQuery] string? sourceId, [FromQuery] string? targetId)
    {
        using var conn = _db.Open();
        var sql = """
            SELECT r.*,
                   s.name AS source_name, s.typ AS source_typ,
                   t.name AS target_name, t.typ AS target_typ
            FROM   infra_relations r
            JOIN   infra_items s ON s.id = r.source_id
            JOIN   infra_items t ON t.id = r.target_id
            WHERE  1=1
            """;
        var p = new DynamicParameters();
        if (!string.IsNullOrEmpty(sourceId)) { sql += " AND r.source_id = @sourceId"; p.Add("sourceId", sourceId); }
        if (!string.IsNullOrEmpty(targetId)) { sql += " AND r.target_id = @targetId"; p.Add("targetId", targetId); }
        sql += " ORDER BY r.source_id, r.target_id";
        return Ok(conn.Query<InfraRelation>(sql, p));
    }

    // POST /api/infra/relations
    [HttpPost("relations")]
    public IActionResult CreateRelation([FromBody] InfraRelationCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.SourceId) || string.IsNullOrWhiteSpace(dto.TargetId)
            || string.IsNullOrWhiteSpace(dto.RelTyp))
            return BadRequest(new { error = "source_id, target_id, rel_typ sind Pflicht" });

        var now = DateTime.UtcNow.ToString("o");
        try
        {
            using var conn = _db.Open();
            conn.Execute("""
                INSERT INTO infra_relations (source_id, target_id, rel_typ, notiz, created_at)
                VALUES (@SourceId, @TargetId, @RelTyp, @Notiz, @Now)
                """,
                new { dto.SourceId, dto.TargetId, dto.RelTyp, Notiz = dto.Notiz ?? "", Now = now });
            return Created("/api/infra/relations", new { ok = true });
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return Conflict(new { error = "Relation bereits vorhanden" });
        }
    }

    // DELETE /api/infra/relations/{id}
    [HttpDelete("relations/{id:int}")]
    public IActionResult DeleteRelation(int id)
    {
        using var conn = _db.Open();
        conn.Execute("DELETE FROM infra_relations WHERE id = @id", new { id });
        return Ok(new { ok = true });
    }

    // ── Incident ↔ Infra links ────────────────────────────────────────────────

    // GET /api/infra/incident-links?incidentId=INC-0041
    [HttpGet("incident-links")]
    public IActionResult GetIncidentLinks([FromQuery] string? incidentId, [FromQuery] string? infraId)
    {
        using var conn = _db.Open();
        var sql = """
            SELECT ii.*, i.name AS infra_name, i.typ AS infra_typ, i.status AS infra_status
            FROM   incident_infra ii
            JOIN   infra_items i ON i.id = ii.infra_id
            WHERE  1=1
            """;
        var p = new DynamicParameters();
        if (!string.IsNullOrEmpty(incidentId)) { sql += " AND ii.incident_id = @incidentId"; p.Add("incidentId", incidentId); }
        if (!string.IsNullOrEmpty(infraId))    { sql += " AND ii.infra_id    = @infraId";    p.Add("infraId",    infraId);    }
        sql += " ORDER BY ii.created_at DESC";
        return Ok(conn.Query<IncidentInfra>(sql, p));
    }

    // POST /api/infra/incident-links
    [HttpPost("incident-links")]
    public IActionResult LinkIncident([FromBody] IncidentInfra dto)
    {
        var now = DateTime.UtcNow.ToString("o");
        try
        {
            using var conn = _db.Open();
            conn.Execute("""
                INSERT INTO incident_infra (incident_id, infra_id, notiz, created_at)
                VALUES (@IncidentId, @InfraId, @Notiz, @Now)
                """,
                new { dto.IncidentId, dto.InfraId, Notiz = dto.Notiz ?? "", Now = now });
            _audit.Log("incident", dto.IncidentId, "LINK_INFRA", $"InfraId={dto.InfraId}");
            return Created("/api/infra/incident-links", new { ok = true });
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return Conflict(new { error = "Verknüpfung bereits vorhanden" });
        }
    }

    // DELETE /api/infra/incident-links/{id}
    [HttpDelete("incident-links/{id:int}")]
    public IActionResult UnlinkIncident(int id)
    {
        using var conn = _db.Open();
        conn.Execute("DELETE FROM incident_infra WHERE id = @id", new { id });
        return Ok(new { ok = true });
    }

    // ── Release ↔ Infra links ─────────────────────────────────────────────────

    // GET /api/infra/release-links?releaseId=REL-2026-16
    [HttpGet("release-links")]
    public IActionResult GetReleaseLinks([FromQuery] string? releaseId, [FromQuery] string? infraId)
    {
        using var conn = _db.Open();
        var sql = """
            SELECT ri.*, i.name AS infra_name, i.typ AS infra_typ, i.status AS infra_status
            FROM   release_infra ri
            JOIN   infra_items i ON i.id = ri.infra_id
            WHERE  1=1
            """;
        var p = new DynamicParameters();
        if (!string.IsNullOrEmpty(releaseId)) { sql += " AND ri.release_id = @releaseId"; p.Add("releaseId", releaseId); }
        if (!string.IsNullOrEmpty(infraId))   { sql += " AND ri.infra_id   = @infraId";   p.Add("infraId",   infraId);   }
        sql += " ORDER BY ri.created_at DESC";
        return Ok(conn.Query<ReleaseInfra>(sql, p));
    }

    // POST /api/infra/release-links
    [HttpPost("release-links")]
    public IActionResult LinkRelease([FromBody] ReleaseInfra dto)
    {
        var now = DateTime.UtcNow.ToString("o");
        try
        {
            using var conn = _db.Open();
            conn.Execute("""
                INSERT INTO release_infra (release_id, infra_id, notiz, created_at)
                VALUES (@ReleaseId, @InfraId, @Notiz, @Now)
                """,
                new { dto.ReleaseId, dto.InfraId, Notiz = dto.Notiz ?? "", Now = now });
            _audit.Log("release", dto.ReleaseId, "LINK_INFRA", $"InfraId={dto.InfraId}");
            return Created("/api/infra/release-links", new { ok = true });
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return Conflict(new { error = "Verknüpfung bereits vorhanden" });
        }
    }

    // DELETE /api/infra/release-links/{id}
    [HttpDelete("release-links/{id:int}")]
    public IActionResult UnlinkRelease(int id)
    {
        using var conn = _db.Open();
        conn.Execute("DELETE FROM release_infra WHERE id = @id", new { id });
        return Ok(new { ok = true });
    }

    // ── Topology summary (for topology view) ─────────────────────────────────

    // GET /api/infra/topology
    [HttpGet("topology")]
    public IActionResult GetTopology()
    {
        using var conn = _db.Open();
        var items = conn.Query<InfraItem>("SELECT * FROM infra_items ORDER BY typ, name").ToList();
        var relations = conn.Query<InfraRelation>("""
            SELECT r.*, s.name AS source_name, s.typ AS source_typ,
                        t.name AS target_name, t.typ AS target_typ
            FROM infra_relations r
            JOIN infra_items s ON s.id = r.source_id
            JOIN infra_items t ON t.id = r.target_id
            """).ToList();
        var incidentCounts = conn.Query<(string InfraId, int Count)>("""
            SELECT ii.infra_id, COUNT(*) AS count
            FROM   incident_infra ii
            JOIN   incidents i ON i.id = ii.incident_id
            WHERE  i.status != 'Gelöst'
            GROUP  BY ii.infra_id
            """).ToDictionary(x => x.InfraId, x => x.Count);

        return Ok(new { items, relations, incident_counts = incidentCounts });
    }
}
