namespace ITSMPro.Models;

// ── Incident ─────────────────────────────────────────────────────────────────
public class Incident
{
    public string  Id          { get; set; } = "";
    public string  Title       { get; set; } = "";
    public string  Prio        { get; set; } = "";
    public string  Status      { get; set; } = "Offen";
    public string  Service     { get; set; } = "";
    public string  Owner       { get; set; } = "";
    public string? Channel     { get; set; }
    public string? ReleaseId   { get; set; }
    public string? Description { get; set; }
    public string? Sla         { get; set; }
    public string  CreatedAt   { get; set; } = "";
    public string  UpdatedAt   { get; set; } = "";
}

public class IncidentCreateDto
{
    public string  Id          { get; set; } = "";
    public string  Title       { get; set; } = "";
    public string  Prio        { get; set; } = "";
    public string? Status      { get; set; }
    public string  Service     { get; set; } = "";
    public string  Owner       { get; set; } = "";
    public string? Channel     { get; set; }
    public string? ReleaseId   { get; set; }
    public string? Description { get; set; }
    public List<string> InfraIds { get; set; } = new();
}

public class IncidentUpdateDto
{
    public string? Status      { get; set; }
    public string? Owner       { get; set; }
    public string? Description { get; set; }
}

// ── Release ──────────────────────────────────────────────────────────────────
public class Release
{
    public string  Id            { get; set; } = "";
    public string  Name          { get; set; } = "";
    public string  Typ           { get; set; } = "";
    public string  Risiko        { get; set; } = "";
    public string  Status        { get; set; } = "Offen";
    public string? Environment   { get; set; }
    public string? Notes         { get; set; }
    public string? Services      { get; set; }
    public string? Rollback      { get; set; }
    public string? TestEvidence  { get; set; }
    public string? KnownRisks    { get; set; }
    public string? Approver      { get; set; }
    public string? ApproveDate   { get; set; }
    public string? ApproveReason { get; set; }
    public string? DateTest      { get; set; }
    public string? DateAbnahme   { get; set; }
    public string? DateProd      { get; set; }
    public string  CreatedAt     { get; set; } = "";
    public string  UpdatedAt     { get; set; } = "";
}

public class ReleaseCreateDto
{
    public string  Id           { get; set; } = "";
    public string  Name         { get; set; } = "";
    public string  Typ          { get; set; } = "";
    public string  Risiko       { get; set; } = "";
    public string? Status       { get; set; }
    public string? Environment  { get; set; }
    public string  Notes        { get; set; } = "";
    public string  Services     { get; set; } = "";
    public string? Rollback     { get; set; }
    public string? TestEvidence { get; set; }
    public string? KnownRisks   { get; set; }
    public string? DateTest     { get; set; }
    public string? DateAbnahme  { get; set; }
    public string? DateProd     { get; set; }
    public List<string> InfraIds { get; set; } = new();
}

public class ReleaseApproveDto
{
    public string? Approver { get; set; }
    public string? Reason   { get; set; }
    public string? Status   { get; set; }
}

// ── Problem / RCA ─────────────────────────────────────────────────────────────
public class Problem
{
    public string  Id          { get; set; } = "";
    public string? IncidentId  { get; set; }
    public string  ProblemType { get; set; } = "Normal";
    public string? RootCause   { get; set; }
    public string  Method      { get; set; } = "5why";
    public string? Why1        { get; set; }
    public string? Why2        { get; set; }
    public string? Why3        { get; set; }
    public string? Lessons     { get; set; }
    public string  Status      { get; set; } = "Offen";
    public string  CreatedAt   { get; set; } = "";
    public string  UpdatedAt   { get; set; } = "";
}

public class ProblemCreateDto
{
    public string? IncidentId  { get; set; }
    public string? ProblemType { get; set; }
    public string? RootCause   { get; set; }
    public string? Method      { get; set; }
    public string? Why1        { get; set; }
    public string? Why2        { get; set; }
    public string? Why3        { get; set; }
    public string? Lessons     { get; set; }
}

// ── Infrastructure Items ──────────────────────────────────────────────────────
public class InfraItem
{
    public string  Id           { get; set; } = "";
    public string  Name         { get; set; } = "";
    public string  Typ          { get; set; } = "";
    public string  Status       { get; set; } = "Aktiv";
    public string? Umgebung     { get; set; }
    public string? Owner        { get; set; }
    public string? Beschreibung { get; set; }
    public string? IpAdresse    { get; set; }
    public string? Version      { get; set; }
    public string? LogPfad      { get; set; }
    public string  CreatedAt    { get; set; } = "";
    public string  UpdatedAt    { get; set; } = "";
}

public class InfraItemCreateDto
{
    public string  Id           { get; set; } = "";
    public string  Name         { get; set; } = "";
    public string  Typ          { get; set; } = "";
    public string? Status       { get; set; }
    public string? Umgebung     { get; set; }
    public string? Owner        { get; set; }
    public string? Beschreibung { get; set; }
    public string? IpAdresse    { get; set; }
    public string? Version      { get; set; }
    public string? LogPfad      { get; set; }
}

public class InfraItemUpdateDto
{
    public string? Status       { get; set; }
    public string? Owner        { get; set; }
    public string? Beschreibung { get; set; }
    public string? IpAdresse    { get; set; }
    public string? Version      { get; set; }
    public string? LogPfad      { get; set; }
}

public class InfraRelation
{
    public int    Id        { get; set; }
    public string SourceId  { get; set; } = "";
    public string TargetId  { get; set; } = "";
    public string RelTyp    { get; set; } = "";
    public string? Notiz    { get; set; }
    public string CreatedAt { get; set; } = "";
    // enriched fields
    public string? SourceName { get; set; }
    public string? SourceTyp  { get; set; }
    public string? TargetName { get; set; }
    public string? TargetTyp  { get; set; }
}

public class InfraRelationCreateDto
{
    public string  SourceId { get; set; } = "";
    public string  TargetId { get; set; } = "";
    public string  RelTyp   { get; set; } = "";
    public string? Notiz    { get; set; }
}

public class IncidentInfra
{
    public int    Id           { get; set; }
    public string IncidentId   { get; set; } = "";
    public string InfraId      { get; set; } = "";
    public string? Notiz       { get; set; }
    public string CreatedAt    { get; set; } = "";
    public string? InfraName   { get; set; }
    public string? InfraTyp    { get; set; }
    public string? InfraStatus { get; set; }
}

public class ReleaseInfra
{
    public int    Id           { get; set; }
    public string ReleaseId    { get; set; } = "";
    public string InfraId      { get; set; } = "";
    public string? Notiz       { get; set; }
    public string CreatedAt    { get; set; } = "";
    public string? InfraName   { get; set; }
    public string? InfraTyp    { get; set; }
    public string? InfraStatus { get; set; }
}

public class InfraDetail
{
    public InfraItem           Item      { get; set; } = new();
    public List<InfraRelation> Relations { get; set; } = new();
    public List<Incident>      Incidents { get; set; } = new();
    public List<Release>       Releases  { get; set; } = new();
}

// ── Audit ─────────────────────────────────────────────────────────────────────
public class AuditEntry
{
    public int    Id         { get; set; }
    public string EntityType { get; set; } = "";
    public string EntityId   { get; set; } = "";
    public string Action     { get; set; } = "";
    public string Actor      { get; set; } = "System";
    public string? Detail    { get; set; }
    public string Ts         { get; set; } = "";
}

// ── KPIs ──────────────────────────────────────────────────────────────────────
public class KpiResult
{
    public int    OpenP1             { get; set; }
    public int    OpenP2             { get; set; }
    public int    OpenP3             { get; set; }
    public int    TotalIncidents     { get; set; }
    public int    Solved             { get; set; }
    public int    SlaErfuellung      { get; set; } = 87;
    public int    MttrMin            { get; set; } = 42;
    public double MtbfDays           { get; set; } = 8.2;
    public int    ReleaseSuccessRate { get; set; }
    public int    ReworkRate         { get; set; } = 12;
    public double AvgHandover        { get; set; } = 1.8;
    public int    DqPflichtfelder    { get; set; } = 96;
    public int    DqServiceLink      { get; set; } = 94;
}

// ── Log Parsing ───────────────────────────────────────────────────────────────
public class LogEntry
{
    public int     Id       { get; set; }
    public string  LogFile  { get; set; } = "";
    public string  LogLine  { get; set; } = "";
    public int     HttpCode { get; set; }
    public string? Method   { get; set; }
    public string? Path     { get; set; }
    public string? Ip       { get; set; }
    public string? Ts       { get; set; }
    public string  ParsedAt { get; set; } = "";
}

public class LogStat
{
    public string  LogFile  { get; set; } = "";
    public int     HttpCode { get; set; }
    public int     Count    { get; set; }
    public string? LastSeen { get; set; }
}

public class LogParseRequest
{
    public string LogFile   { get; set; } = "";
    public int    SinceLine { get; set; } = 0;
}

public class LogParseResult
{
    public bool   Ok         { get; set; } = true;
    public int    NewEntries { get; set; }
    public int    TotalLines { get; set; }
    public int    SinceLine  { get; set; }
    public Dictionary<string, LogStatDetail> Stats  { get; set; } = new();
    public List<LogEntryDto>                 Recent { get; set; } = new();
}

public class LogStatDetail
{
    public int    Count    { get; set; }
    public string LastSeen { get; set; } = "";
}

public class LogEntryDto
{
    public int     HttpCode { get; set; }
    public string? Method   { get; set; }
    public string? Path     { get; set; }
    public string? Ip       { get; set; }
    public string? Ts       { get; set; }
}
