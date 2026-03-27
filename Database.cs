using Dapper;
using Microsoft.Data.Sqlite;

namespace ITSMPro.Data;

public class Database
{
    private readonly string _connectionString;

    public Database(IConfiguration cfg)
    {
        var dbPath = cfg["Database:Path"] ?? Path.Combine(
            AppContext.BaseDirectory, "itsm.db");
        _connectionString = $"Data Source={dbPath}";
    }

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        conn.Execute("PRAGMA journal_mode=WAL;");
        conn.Execute("PRAGMA foreign_keys=ON;");
        return conn;
    }

    public void EnsureSchema()
    {
        using var conn = Open();

        // ── Core tables ───────────────────────────────────────────────────────
        conn.Execute("""
            CREATE TABLE IF NOT EXISTS incidents (
                id          TEXT PRIMARY KEY,
                title       TEXT NOT NULL,
                prio        TEXT NOT NULL,
                status      TEXT NOT NULL DEFAULT 'Offen',
                service     TEXT NOT NULL,
                owner       TEXT NOT NULL,
                channel     TEXT,
                release_id  TEXT,
                description TEXT,
                sla         TEXT,
                created_at  TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );
        """);

        conn.Execute("""
            CREATE TABLE IF NOT EXISTS releases (
                id             TEXT PRIMARY KEY,
                name           TEXT NOT NULL,
                typ            TEXT NOT NULL,
                risiko         TEXT NOT NULL,
                status         TEXT NOT NULL DEFAULT 'Offen',
                environment    TEXT DEFAULT 'Dev',
                notes          TEXT,
                services       TEXT,
                rollback       TEXT,
                test_evidence  TEXT,
                known_risks    TEXT,
                approver       TEXT,
                approve_date   TEXT,
                approve_reason TEXT,
                date_test      TEXT,
                date_abnahme   TEXT,
                date_prod      TEXT,
                created_at     TEXT NOT NULL,
                updated_at     TEXT NOT NULL
            );
        """);

        // Migrations for existing DBs
        try { conn.Execute("ALTER TABLE releases ADD COLUMN date_test    TEXT"); } catch { }
        try { conn.Execute("ALTER TABLE releases ADD COLUMN date_abnahme TEXT"); } catch { }
        try { conn.Execute("ALTER TABLE releases ADD COLUMN date_prod    TEXT"); } catch { }

        conn.Execute("""
            CREATE TABLE IF NOT EXISTS problems (
                id           TEXT PRIMARY KEY,
                incident_id  TEXT,
                problem_type TEXT DEFAULT 'Normal',
                root_cause   TEXT,
                method       TEXT DEFAULT '5why',
                why1         TEXT,
                why2         TEXT,
                why3         TEXT,
                lessons      TEXT,
                status       TEXT DEFAULT 'Offen',
                created_at   TEXT NOT NULL,
                updated_at   TEXT NOT NULL,
                FOREIGN KEY (incident_id) REFERENCES incidents(id)
            );
        """);

        conn.Execute("""
            CREATE TABLE IF NOT EXISTS audit_log (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                entity_type TEXT NOT NULL,
                entity_id   TEXT NOT NULL,
                action      TEXT NOT NULL,
                actor       TEXT DEFAULT 'System',
                detail      TEXT,
                ts          TEXT NOT NULL
            );
        """);

        conn.Execute("""
            CREATE TABLE IF NOT EXISTS log_entries (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                log_file   TEXT NOT NULL,
                log_line   TEXT NOT NULL,
                http_code  INTEGER,
                method     TEXT,
                path       TEXT,
                ip         TEXT,
                ts         TEXT,
                parsed_at  TEXT NOT NULL
            );
        """);

        conn.Execute("""
            CREATE TABLE IF NOT EXISTS log_stats (
                id        INTEGER PRIMARY KEY AUTOINCREMENT,
                log_file  TEXT NOT NULL,
                http_code INTEGER NOT NULL,
                count     INTEGER DEFAULT 0,
                last_seen TEXT,
                UNIQUE(log_file, http_code)
            );
        """);

        // ── Infrastructure tables ─────────────────────────────────────────────
        conn.Execute("""
            CREATE TABLE IF NOT EXISTS infra_items (
                id           TEXT PRIMARY KEY,
                name         TEXT NOT NULL,
                typ          TEXT NOT NULL,
                status       TEXT NOT NULL DEFAULT 'Aktiv',
                umgebung     TEXT,
                owner        TEXT,
                beschreibung TEXT,
                ip_adresse   TEXT,
                version      TEXT,
                log_pfad     TEXT,
                created_at   TEXT NOT NULL,
                updated_at   TEXT NOT NULL
            );
        """);

        // n:m relations between infra items (Firewall → Service, LB → Service, etc.)
        conn.Execute("""
            CREATE TABLE IF NOT EXISTS infra_relations (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                source_id  TEXT NOT NULL,
                target_id  TEXT NOT NULL,
                rel_typ    TEXT NOT NULL,
                notiz      TEXT,
                created_at TEXT NOT NULL,
                FOREIGN KEY (source_id) REFERENCES infra_items(id),
                FOREIGN KEY (target_id) REFERENCES infra_items(id),
                UNIQUE(source_id, target_id, rel_typ)
            );
        """);

        // Incidents ↔ Infra items
        conn.Execute("""
            CREATE TABLE IF NOT EXISTS incident_infra (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                incident_id TEXT NOT NULL,
                infra_id    TEXT NOT NULL,
                notiz       TEXT,
                created_at  TEXT NOT NULL,
                FOREIGN KEY (incident_id) REFERENCES incidents(id),
                FOREIGN KEY (infra_id)    REFERENCES infra_items(id),
                UNIQUE(incident_id, infra_id)
            );
        """);

        // Releases ↔ Infra items
        conn.Execute("""
            CREATE TABLE IF NOT EXISTS release_infra (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                release_id  TEXT NOT NULL,
                infra_id    TEXT NOT NULL,
                notiz       TEXT,
                created_at  TEXT NOT NULL,
                FOREIGN KEY (release_id) REFERENCES releases(id),
                FOREIGN KEY (infra_id)   REFERENCES infra_items(id),
                UNIQUE(release_id, infra_id)
            );
        """);
    }

    public void SeedDemoData()
    {
        using var conn = Open();

        var existingCount = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM incidents");
        if (existingCount > 0) return;

        var now = DateTime.UtcNow.ToString("o");

        // ── Incidents ─────────────────────────────────────────────────────────
        conn.Execute("""
            INSERT INTO incidents VALUES
              ('INC-0041','Streaming-Plattform: API nicht erreichbar','P1','In Bearbeitung','Release Hub','2nd Line — Backend-Team','Monitoring-Alert (automatisch)','REL-2026-16','Kritischer API-Ausfall auf Produktivumgebung','12 min verbleibend',@now,@now),
              ('INC-0039','Login-Service: Timeout bei OAuth','P1','Offen','Release Hub','1st Line — Service Desk','Slack','REL-2026-16','OAuth-Token-Validierung schlägt fehl','5 min verbleibend',@now,@now),
              ('INC-0037','Monitoring-Alert: DB-Latenz erhöht','P2','In Bearbeitung','Ops Center','3rd Line — Plattform-Team','Monitoring-Alert (automatisch)','REL-2026-15','Latenz > 500ms auf Primary-DB','45 min verbleibend',@now,@now),
              ('INC-0035','Reporting: Exportfehler bei großen Datensätzen','P3','In Bearbeitung','Ops Center','2nd Line — Backend-Team','Ticket-Portal','','Export schlägt ab 50.000 Zeilen fehl','4h verbleibend',@now,@now),
              ('INC-0033','CI/CD-Pipeline: Build fehlgeschlagen','P2','Gelöst','Release Hub','3rd Line — Plattform-Team','Slack','REL-2026-13','Jenkins-Build Stage 3 Fehler','SLA eingehalten',@now,@now)
            """, new { now });

        // ── Releases ──────────────────────────────────────────────────────────
        conn.Execute("""
            INSERT INTO releases
                (id,name,typ,risiko,status,environment,notes,services,rollback,test_evidence,known_risks,approver,approve_date,approve_reason,date_test,date_abnahme,date_prod,created_at,updated_at)
            VALUES
              ('REL-2026-17','Login-Service v2.1.0','Emergency','Hoch','Abgeschlossen','Prod','Hotfix: Session-Timeout-Bug behoben. Behebt INC-0039.','Release Hub','Helm rollback auf v2.0.9','Smoke Tests ✓','Session-Invalidierung möglich','K. Bauer','14.03.2026 08:15','Kritischer Hotfix INC-0039','12.03.2026','13.03.2026','14.03.2026',@now,@now),
              ('REL-2026-16','Auth-Service v3.2.1','Emergency','Hoch','Deployment','Stage','OAuth-Token-Validierung überarbeitet. Behebt CVE-2026-0412.','Release Hub','Helm rollback auf v3.2.0','Unit + Integration Tests','Potenzielle Session-Invalidierung','J. Weber','14.03.2026 09:41','Sicherheitslücke CVE-2026-0412','13.03.2026','14.03.2026','15.03.2026',@now,@now),
              ('REL-2026-15','Reporting v2.4.0','Standard','Mittel','In Review','Test','Exportfunktion überarbeitet, neue CSV-Formate.','Ops Center','DB-Snapshot von 12.03.','Unit Tests','Keine bekannten','','','','18.03.2026','20.03.2026','22.03.2026',@now,@now),
              ('REL-2026-14','DB-Migration v1.9','Normal','Niedrig','Abgeschlossen','Prod','Index-Optimierung für Query-Performance.','Release Hub, Ops Center','Migration-Skript revertiert','Staging-Deployment','Keine','M. Schmidt','07.03.2026 14:00','Performance-Verbesserung','05.03.2026','06.03.2026','07.03.2026',@now,@now),
              ('REL-2026-13','CI-Pipeline Update','Standard','Mittel','Abgeschlossen','Prod','Jenkins-Upgrade auf v2.440.','Release Hub','Downgrade auf v2.430','Manuell getestet','Keine','K. Bauer','03.03.2026 10:15','Sicherheits-Update Jenkins','01.03.2026','02.03.2026','03.03.2026',@now,@now)
            """, new { now });

        // ── Infra Items ───────────────────────────────────────────────────────
        conn.Execute("""
            INSERT INTO infra_items
                (id,name,typ,status,umgebung,owner,beschreibung,ip_adresse,version,log_pfad,created_at,updated_at)
            VALUES
              ('SVC-RH','Release Hub','Service','Aktiv','Prod','2nd Line — Backend-Team','Masterarbeit Mini-App 1 — Release-Pipeline Management','192.168.10.10','1.4.2','C:\Users\Manuel\Desktop\FH_Master\Masterarbeit\Implementierung\masterarbeit_mini_apps\masterarbeit_mini_apps\release_hub\logs\app.log',@now,@now),
              ('SVC-OC','Ops Center','Service','Aktiv','Prod','3rd Line — Plattform-Team','Masterarbeit Mini-App 2 — Operations & Monitoring Center','192.168.10.11','2.1.0','C:\Users\Manuel\Desktop\FH_Master\Masterarbeit\Implementierung\masterarbeit_mini_apps\masterarbeit_mini_apps\ops_center\logs\app.log',@now,@now),
              ('FW-01','Firewall Perimeter','Firewall','Aktiv','Prod','3rd Line — Plattform-Team','Haupt-Perimeter-Firewall, schützt alle Prod-Services','10.0.0.1','FortiGate 7.4','',@now,@now),
              ('FW-02','WAF Cluster','Firewall','Aktiv','Prod','3rd Line — Plattform-Team','Web Application Firewall vor API-Services','10.0.0.2','ModSecurity 3.0','',@now,@now),
              ('LB-01','Load Balancer Primary','Loadbalancer','Aktiv','Prod','3rd Line — Plattform-Team','Primärer Load Balancer — Round Robin auf Release Hub','10.0.1.1','nginx 1.24','',@now,@now),
              ('LB-02','Load Balancer Secondary','Loadbalancer','Aktiv','Prod','3rd Line — Plattform-Team','Sekundärer Load Balancer — Failover für Ops Center','10.0.1.2','nginx 1.24','',@now,@now),
              ('DB-01','PostgreSQL Primary','Datenbank','Aktiv','Prod','3rd Line — Plattform-Team','Primäre Produktionsdatenbank','192.168.20.10','PostgreSQL 16.2','',@now,@now),
              ('DB-02','PostgreSQL Replica','Datenbank','Aktiv','Prod','3rd Line — Plattform-Team','Read Replica für Reporting & Ops Center','192.168.20.11','PostgreSQL 16.2','',@now,@now),
              ('DB-03','Redis Cache','Datenbank','Aktiv','Prod','2nd Line — Backend-Team','Session Cache und Queue','192.168.20.20','Redis 7.2','',@now,@now),
              ('SRV-01','App Server 1','Server','Aktiv','Prod','3rd Line — Plattform-Team','Kubernetes Node 1','192.168.30.10','K8s 1.29','',@now,@now),
              ('SRV-02','App Server 2','Server','Aktiv','Prod','3rd Line — Plattform-Team','Kubernetes Node 2','192.168.30.11','K8s 1.29','',@now,@now),
              ('MON-01','Prometheus / Grafana','Monitoring','Aktiv','Prod','3rd Line — Plattform-Team','Zentrales Monitoring Stack','192.168.40.10','Grafana 10.4','',@now,@now),
              ('MON-02','Alertmanager','Monitoring','Aktiv','Prod','3rd Line — Plattform-Team','Alert-Routing und Eskalation','192.168.40.11','v0.27','',@now,@now),
              ('CICD-01','Jenkins Master','CI/CD','Aktiv','Prod','3rd Line — Plattform-Team','CI/CD Pipeline Server','192.168.50.10','Jenkins 2.440','',@now,@now),
              ('NET-01','Core Switch','Netzwerk','Aktiv','Prod','3rd Line — Plattform-Team','Kern-Switch Datacenter','192.168.1.1','Cisco C9300','',@now,@now),
              ('STORE-01','NAS Storage','Storage','Aktiv','Prod','3rd Line — Plattform-Team','Zentraler Netzwerk-Storage','192.168.60.10','NetApp ONTAP 9.14','',@now,@now)
            """, new { now });

        // ── Infra Relations (Topologie) ────────────────────────────────────────
        conn.Execute("""
            INSERT INTO infra_relations (source_id,target_id,rel_typ,notiz,created_at) VALUES
              ('FW-01','LB-01','schützt','Perimeter-Firewall filtert Traffic vor LB',@now),
              ('FW-01','LB-02','schützt','Perimeter-Firewall filtert Traffic vor LB',@now),
              ('FW-02','SVC-RH','schützt','WAF schützt Release Hub API',@now),
              ('FW-02','SVC-OC','schützt','WAF schützt Ops Center API',@now),
              ('LB-01','SVC-RH','routet zu','Load Balancer verteilt Traffic auf Release Hub',@now),
              ('LB-02','SVC-OC','routet zu','Load Balancer verteilt Traffic auf Ops Center',@now),
              ('SRV-01','SVC-RH','hostet','App Server 1 hostet Release Hub Container',@now),
              ('SRV-02','SVC-OC','hostet','App Server 2 hostet Ops Center Container',@now),
              ('DB-01','SVC-RH','hängt ab von','Release Hub nutzt PostgreSQL Primary als Hauptdatenbank',@now),
              ('DB-01','SVC-OC','hängt ab von','Ops Center nutzt PostgreSQL Primary',@now),
              ('DB-02','SVC-OC','repliziert','Read Replica für Ops Center Reporting',@now),
              ('DB-03','SVC-RH','hängt ab von','Release Hub nutzt Redis für Session-Cache',@now),
              ('MON-01','SVC-RH','überwacht','Prometheus scrapt Release Hub Metrics',@now),
              ('MON-01','SVC-OC','überwacht','Prometheus scrapt Ops Center Metrics',@now),
              ('MON-02','MON-01','verbindet','Alertmanager empfängt Alerts von Prometheus',@now),
              ('CICD-01','SVC-RH','verbindet','Jenkins deployt auf Release Hub',@now),
              ('CICD-01','SVC-OC','verbindet','Jenkins deployt auf Ops Center',@now),
              ('NET-01','SRV-01','verbindet','Core Switch verbindet App Server 1',@now),
              ('NET-01','SRV-02','verbindet','Core Switch verbindet App Server 2',@now),
              ('STORE-01','SRV-01','verbindet','NAS Storage für App Server 1',@now),
              ('STORE-01','SRV-02','verbindet','NAS Storage für App Server 2',@now)
            """, new { now });

        // ── Incident ↔ Infra links ─────────────────────────────────────────────
        conn.Execute("""
            INSERT INTO incident_infra (incident_id,infra_id,notiz,created_at) VALUES
              ('INC-0041','SVC-RH','Primär betroffener Service',@now),
              ('INC-0041','LB-01','Load Balancer meldet Connection Refused',@now),
              ('INC-0041','FW-02','WAF Logs zeigen erhöhte 502-Fehler',@now),
              ('INC-0039','SVC-RH','OAuth-Endpunkt im Release Hub betroffen',@now),
              ('INC-0039','DB-03','Redis Session-Store Timeout',@now),
              ('INC-0037','SVC-OC','Ops Center DB-Monitoring hat Alert ausgelöst',@now),
              ('INC-0037','DB-01','PostgreSQL Primary zeigt hohe Latenz',@now),
              ('INC-0037','MON-01','Monitoring-Alert ausgelöst',@now),
              ('INC-0035','SVC-OC','Reporting-Export betroffen',@now),
              ('INC-0035','DB-02','Read Replica unter Last',@now),
              ('INC-0033','CICD-01','Jenkins Build fehlgeschlagen',@now)
            """, new { now });

        // ── Release ↔ Infra links ──────────────────────────────────────────────
        conn.Execute("""
            INSERT INTO release_infra (release_id,infra_id,notiz,created_at) VALUES
              ('REL-2026-17','SVC-RH','Hotfix Deployment',@now),
              ('REL-2026-17','DB-03','Redis Session-Flush',@now),
              ('REL-2026-16','SVC-RH','Primärer Deployment-Ziel',@now),
              ('REL-2026-16','LB-01','LB Config-Update erforderlich',@now),
              ('REL-2026-16','DB-03','Redis Session-Keys werden geleert',@now),
              ('REL-2026-15','SVC-OC','Reporting-Modul Update',@now),
              ('REL-2026-15','DB-02','Read Replica Index-Anpassung',@now),
              ('REL-2026-14','DB-01','PostgreSQL Schema-Migration',@now),
              ('REL-2026-14','DB-02','Replica Resync nach Migration',@now),
              ('REL-2026-13','CICD-01','Jenkins selbst wird aktualisiert',@now),
              ('REL-2026-13','SRV-01','Deployment auf App Server 1',@now)
            """, new { now });
    }
}
