using Dapper;
using ITSMPro.Data;
using ITSMPro.Models;

namespace ITSMPro.Services;

public class AuditService
{
    private readonly Database _db;

    public AuditService(Database db) => _db = db;

    public void Log(string entityType, string entityId, string action,
                    string detail = "", string actor = "Anna Müller")
    {
        using var conn = _db.Open();
        conn.Execute("""
            INSERT INTO audit_log (entity_type, entity_id, action, actor, detail, ts)
            VALUES (@EntityType, @EntityId, @Action, @Actor, @Detail, @Ts)
            """,
            new
            {
                EntityType = entityType,
                EntityId   = entityId,
                Action     = action,
                Actor      = actor,
                Detail     = detail,
                Ts         = DateTime.UtcNow.ToString("o")
            });
    }

    public List<AuditEntry> GetForEntity(string entityId, int limit = 100)
    {
        using var conn = _db.Open();
        return conn.Query<AuditEntry>("""
            SELECT id, entity_type AS EntityType, entity_id AS EntityId,
                   action AS Action, actor AS Actor, detail AS Detail, ts AS Ts
            FROM   audit_log
            WHERE  entity_id = @entityId
            ORDER  BY ts DESC
            LIMIT  @limit
            """, new { entityId, limit }).ToList();
    }

    public List<AuditEntry> GetAll(int limit = 100)
    {
        using var conn = _db.Open();
        return conn.Query<AuditEntry>("""
            SELECT id, entity_type AS EntityType, entity_id AS EntityId,
                   action AS Action, actor AS Actor, detail AS Detail, ts AS Ts
            FROM   audit_log
            ORDER  BY ts DESC
            LIMIT  @limit
            """, new { limit }).ToList();
    }
}
