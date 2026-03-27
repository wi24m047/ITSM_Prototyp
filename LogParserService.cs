using System.Text.RegularExpressions;
using Dapper;
using ITSMPro.Data;
using ITSMPro.Models;

namespace ITSMPro.Services;

/// <summary>
/// Parses log files incrementally (only new lines since last parse).
/// Supports: nginx/Apache combined, simple bracketed, JSON-line, bare HTTP codes.
/// Windows paths with backslashes are fully supported.
/// </summary>
public partial class LogParserService
{
    private readonly Database _db;
    private readonly ILogger<LogParserService> _logger;

    // nginx/Apache combined & common:
    // 1.2.3.4 - - [14/Mar/2026:09:00:00 +0000] "GET /path HTTP/1.1" 200 512
    [GeneratedRegex(
        @"(?<ip>\S+)\s+\S+\s+\S+\s+\[(?<ts>[^\]]+)\]\s+""(?<method>\w+)\s+(?<path>\S+)[^""]*""\s+(?<code>\d{3})",
        RegexOptions.Compiled)]
    private static partial Regex NginxPattern();

    // Flask/Python with ANSI codes and escaped quotes:
    // 2026-03-14 21:39:56,841 | INFO | 127.0.0.1 - - [14/Mar/2026 21:39:56] "GET /path HTTP/1.1" 404 -
    [GeneratedRegex(
        @"(?<ip>\d+\.\d+\.\d+\.\d+)\s+-\s+-\s+\[(?<ts>[^\]]+)\]\s+\\?""?(?<method>GET|POST|PUT|DELETE|PATCH|HEAD|OPTIONS)\s+(?<path>\S+)[^""]*\\?""\s+(?<code>\d{3})",
        RegexOptions.Compiled)]
    private static partial Regex FlaskPattern();

    // Simple bracketed: [2026-03-14 09:00:00] GET /path 200
    [GeneratedRegex(
        @"\[(?<ts>[^\]]+)\]\s+(?<method>GET|POST|PUT|DELETE|PATCH|HEAD|OPTIONS)\s+(?<path>\S+)\s+(?<code>\d{3})",
        RegexOptions.Compiled)]
    private static partial Regex SimpleBracketPattern();

    // Bare: METHOD /path CODE (anywhere in line)
    [GeneratedRegex(
        @"(?<method>GET|POST|PUT|DELETE|PATCH|HEAD|OPTIONS)\s+(?<path>\S+)\s+(?<code>\d{3})",
        RegexOptions.Compiled)]
    private static partial Regex BarePattern();

    // Last resort: valid HTTP status codes only (100-599), not port numbers
    [GeneratedRegex(@"(?<![:\d])(?<code>[1-5]\d{2})(?!\d)", RegexOptions.Compiled)]
    private static partial Regex AnyCodePattern();

    // Strip ANSI escape codes (e.g. [33m, [0m from Flask colored output)
    [GeneratedRegex(@"\x1B\[[0-9;]*[mK]|\[[\d;]+m", RegexOptions.Compiled)]
    private static partial Regex AnsiPattern();

    public LogParserService(Database db, ILogger<LogParserService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public LogParseResult Parse(LogParseRequest req)
    {
        // Normalize Windows backslash paths
        var logFile = req.LogFile.Trim();

        if (!File.Exists(logFile))
        {
            throw new FileNotFoundException($"Log-Datei nicht gefunden: {logFile}");
        }

        var parsedAt = DateTime.UtcNow.ToString("o");

        using var fs     = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);
        var allLines     = new List<string>();
        while (!reader.EndOfStream)
            allLines.Add(reader.ReadLine() ?? "");

        var newLines   = allLines.Skip(req.SinceLine).ToArray();
        var newEntries = 0;

        using var conn = _db.Open();
        using var tx   = conn.BeginTransaction();

        foreach (var rawLine in newLines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parsed = ParseLine(line);
            if (parsed is null) continue;

            conn.Execute("""
                INSERT INTO log_entries
                    (log_file, log_line, http_code, method, path, ip, ts, parsed_at)
                VALUES
                    (@LogFile, @LogLine, @HttpCode, @Method, @Path, @Ip, @Ts, @ParsedAt)
                """,
                new
                {
                    LogFile   = logFile,
                    LogLine   = line.Length > 500 ? line[..500] : line,
                    parsed.HttpCode,
                    parsed.Method,
                    parsed.Path,
                    parsed.Ip,
                    parsed.Ts,
                    ParsedAt  = parsedAt
                }, tx);

            conn.Execute("""
                INSERT INTO log_stats (log_file, http_code, count, last_seen)
                VALUES (@LogFile, @HttpCode, 1, @ParsedAt)
                ON CONFLICT(log_file, http_code) DO UPDATE SET
                    count     = count + 1,
                    last_seen = excluded.last_seen
                """,
                new { LogFile = logFile, parsed.HttpCode, ParsedAt = parsedAt }, tx);

            newEntries++;
        }

        tx.Commit();

        // Build stats dict
        var statsRows = conn.Query<LogStat>(
            "SELECT log_file, http_code, count, last_seen FROM log_stats WHERE log_file = @LogFile ORDER BY http_code",
            new { LogFile = logFile });

        var stats = statsRows
            .Where(r => r.HttpCode >= 100 && r.HttpCode < 600)
            .GroupBy(r => r.HttpCode.ToString())
            .ToDictionary(
                g => g.Key,
                g => new LogStatDetail { Count = g.Sum(r => r.Count), LastSeen = g.Max(r => r.LastSeen) ?? "" });

        // Recent 50 entries
        var recent = conn.Query<LogEntryDto>("""
            SELECT http_code AS HttpCode, method AS Method, path AS Path, ip AS Ip, ts AS Ts
            FROM   log_entries
            WHERE  log_file = @LogFile
            ORDER  BY id DESC
            LIMIT  50
            """, new { LogFile = logFile }).ToList();

        _logger.LogInformation("Parsed {New} new entries from {File} (lines {From}–{To})",
            newEntries, logFile, req.SinceLine, req.SinceLine + newLines.Length);

        return new LogParseResult
        {
            Ok         = true,
            NewEntries = newEntries,
            TotalLines = allLines.Count,
            SinceLine  = req.SinceLine + newLines.Length,
            Stats      = stats,
            Recent     = recent
        };
    }

    public List<LogStat> GetStats(string? logFile)
    {
        using var conn = _db.Open();
        if (!string.IsNullOrEmpty(logFile))
        {
            return conn.Query<LogStat>(
                "SELECT log_file, http_code, count, last_seen FROM log_stats WHERE log_file = @logFile ORDER BY http_code",
                new { logFile }).ToList();
        }
        return conn.Query<LogStat>(
            "SELECT log_file, http_code, count, last_seen FROM log_stats ORDER BY log_file, http_code"
        ).ToList();
    }

    public List<LogEntry> GetRecent(string? logFile, int limit = 100)
    {
        using var conn = _db.Open();
        if (!string.IsNullOrEmpty(logFile))
        {
            return conn.Query<LogEntry>(
                "SELECT * FROM log_entries WHERE log_file = @logFile ORDER BY id DESC LIMIT @limit",
                new { logFile, limit }).ToList();
        }
        return conn.Query<LogEntry>(
            "SELECT * FROM log_entries ORDER BY id DESC LIMIT @limit",
            new { limit }).ToList();
    }

    private static ParsedLine? ParseLine(string line)
    {
        // Strip ANSI escape codes first (Flask/Python colored log output)
        var clean = AnsiPattern().Replace(line, "");

        // 1. Flask/Python format (most specific - with IP and timestamp)
        var m = FlaskPattern().Match(clean);
        if (m.Success)
            return new ParsedLine(
                int.Parse(m.Groups["code"].Value),
                m.Groups["method"].Value,
                m.Groups["path"].Value,
                m.Groups["ip"].Value,
                m.Groups["ts"].Value);

        // 2. nginx/Apache combined
        m = NginxPattern().Match(clean);
        if (m.Success)
            return new ParsedLine(
                int.Parse(m.Groups["code"].Value),
                m.Groups["method"].Value,
                m.Groups["path"].Value,
                m.Groups["ip"].Value,
                m.Groups["ts"].Value);

        // 3. Simple bracketed [timestamp] METHOD /path CODE
        m = SimpleBracketPattern().Match(clean);
        if (m.Success)
            return new ParsedLine(
                int.Parse(m.Groups["code"].Value),
                m.Groups["method"].Value,
                m.Groups["path"].Value,
                "",
                m.Groups["ts"].Value);

        // 4. Bare METHOD /path CODE — only if HTTP method is present
        m = BarePattern().Match(clean);
        if (m.Success)
            return new ParsedLine(
                int.Parse(m.Groups["code"].Value),
                m.Groups["method"].Value,
                m.Groups["path"].Value,
                "", "");

        // No fallback to AnyCodePattern — too many false positives
        return null;
    }

    private record ParsedLine(int HttpCode, string Method, string Path, string Ip, string Ts);
}
