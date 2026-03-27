# ITSM·Pro — ASP.NET Core 9 Backend

DSR-Prototyp nach Hevner | ITIL 4 / COBIT | Dapper + SQLite

## Voraussetzungen

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9) (kostenlos)
- Windows 10/11 (Pfade sind bereits konfiguriert)

## Schnellstart

```cmd
cd ITSMPro
dotnet run
```

Das war's. Die Anwendung:
- Erstellt `itsm.db` automatisch im Ausführungsverzeichnis
- Befüllt die DB mit Demo-Daten beim ersten Start
- Startet den API-Server auf **http://localhost:5050**

Dann `itsm_prototype_v2.html` (oder v3) im Browser öffnen.

---

## Projektstruktur

```
ITSMPro/
├── ITSMPro.csproj              NuGet-Pakete (Dapper, Microsoft.Data.Sqlite, Serilog)
├── Program.cs                  App-Start, DI-Container, CORS, DB-Init
├── appsettings.json            DB-Pfad + Log-Pfade für beide Mini-Apps
│
├── Models/
│   └── Models.cs               Alle Domänenmodelle + DTOs
│
├── Data/
│   └── Database.cs             SQLite-Verbindung (Dapper), Schema, Seed
│
├── Services/
│   ├── LogParserService.cs     Log-Parser (4 Regex-Pattern, inkrementell)
│   └── AuditService.cs         Audit-Log Schreiber / Leser
│
└── Controllers/
    ├── IncidentsController.cs  GET/POST/PUT  /api/incidents
    ├── ReleasesController.cs   GET/POST      /api/releases
    │                           POST          /api/releases/{id}/approve
    ├── ProblemsController.cs   GET/POST      /api/problems
    ├── LogsController.cs       POST          /api/logs/parse
    │                           GET           /api/logs/stats
    │                           GET           /api/logs/recent
    └── MiscControllers.cs      GET           /api/audit
                                GET           /api/kpis
                                GET           /api/health
```

---

## API-Endpunkte (identisch zum Flask-Backend)

| Methode | Endpoint                        | Beschreibung                          |
|---------|---------------------------------|---------------------------------------|
| GET     | /api/health                     | Status + DB-Zähler                    |
| GET     | /api/incidents                  | Liste (Filter: prio, status, q)       |
| GET     | /api/incidents/{id}             | Detail + Audit-Trail                  |
| POST    | /api/incidents                  | Neuer Incident (Pflichtfelder geprüft)|
| PUT     | /api/incidents/{id}             | Status/Owner/Beschreibung ändern      |
| GET     | /api/releases                   | Alle Releases                         |
| GET     | /api/releases/{id}              | Release-Detail                        |
| POST    | /api/releases                   | Neuer Release                         |
| POST    | /api/releases/{id}/approve      | Freigabe erteilen (Audit-Log)         |
| GET     | /api/problems                   | Alle RCAs                             |
| POST    | /api/problems                   | Neue RCA                              |
| GET     | /api/audit?entity_id=INC-0041   | Audit-Einträge                        |
| GET     | /api/kpis                       | KPI-Berechnung aus DB                 |
| POST    | /api/logs/parse                 | Log-Datei parsen (inkrementell)       |
| GET     | /api/logs/stats                 | HTTP-Code-Statistiken                 |
| GET     | /api/logs/recent                | Letzte Log-Einträge                   |

---

## Log-Monitor — vorkonfigurierte Pfade

In `appsettings.json` sind beide Pfade bereits eingetragen:

```
App 1 (Release Hub):
C:\Users\Manuel\Desktop\FH_Master\Masterarbeit\Implementierung\
masterarbeit_mini_apps\masterarbeit_mini_apps\release_hub\logs\app.log

App 2 (Ops Center):
C:\Users\Manuel\Desktop\FH_Master\Masterarbeit\Implementierung\
masterarbeit_mini_apps\masterarbeit_mini_apps\ops_center\logs\app.log
```

Im HTML-Frontend (Log-Monitor Seite) gibt es einen Dropdown zum direkten Wechseln zwischen beiden Apps. Der Auto-Refresh liest alle 2 Minuten nur **neue Zeilen** (inkrementell ab `since_line`).

### Unterstützte Log-Formate

| Format          | Beispiel                                                                      |
|-----------------|-------------------------------------------------------------------------------|
| nginx/Apache    | `192.168.1.1 - - [14/Mar/2026:09:00:00 +0000] "GET /api HTTP/1.1" 200 512`  |
| Simple bracketed| `[2026-03-14 09:00:00] GET /api/health 200`                                  |
| Bare            | `POST /api/data 500`                                                          |
| Fallback        | Jede Zeile mit einem 3-stelligen HTTP-Code (1xx–5xx)                          |

---

## SQLite-Datenbank

Die Datei `itsm.db` wird im Ausführungsverzeichnis erstellt.
Pfad konfigurierbar in `appsettings.json` → `Database:Path`.

Tabellen:
- `incidents` — alle Incidents mit Prio, SLA, Traceability
- `releases` — Releases mit Workflow-Status und Freigabe
- `problems` — RCAs mit 5-Why / Ishikawa
- `audit_log` — tamper-evidentes Log aller Aktionen (C2)
- `log_entries` — geparste Log-Zeilen beider Apps
- `log_stats` — aggregierte HTTP-Code-Zähler je Log-Datei

---

## NuGet-Pakete

| Paket                    | Version | Zweck                    |
|--------------------------|---------|--------------------------|
| Dapper                   | 2.1.35  | Leichtgewichtiger ORM    |
| Microsoft.Data.Sqlite    | 9.0.2   | SQLite-Treiber für .NET  |
| Serilog.AspNetCore       | 8.0.3   | Structured Logging       |
| Serilog.Sinks.Console    | 6.0.0   | Konsolen-Output          |
| Serilog.Sinks.File       | 6.0.0   | Rolling File Logs        |
