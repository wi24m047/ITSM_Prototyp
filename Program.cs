using ITSMPro.Data;
using ITSMPro.Services;
using Serilog;

// ── Serilog early init ────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/itsmpro-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Logging ───────────────────────────────────────────────────────────────
    builder.Host.UseSerilog();

    // ── Services ──────────────────────────────────────────────────────────────
    builder.Services.AddControllers()
        .AddJsonOptions(opts =>
        {
            // camelCase JSON output to match Flask responses exactly
            opts.JsonSerializerOptions.PropertyNamingPolicy =
                System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
        });

    builder.Services.AddEndpointsApiExplorer();

    // Register DB and services as singletons (SQLite is process-local)
    builder.Services.AddSingleton<Database>();
    builder.Services.AddSingleton<AuditService>();
    builder.Services.AddSingleton<LogParserService>();

    // ── CORS — allow the HTML file opened from disk (file://) ─────────────────
    builder.Services.AddCors(opts =>
        opts.AddDefaultPolicy(p =>
            p.AllowAnyOrigin()
             .AllowAnyMethod()
             .AllowAnyHeader()));

    var app = builder.Build();

    // ── Dapper snake_case mapping ─────────────────────────────────────────────
    Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    var db = app.Services.GetRequiredService<Database>();
    db.EnsureSchema();
    db.SeedDemoData();

    // ── Middleware pipeline ───────────────────────────────────────────────────
    app.UseCors();
    app.UseDefaultFiles(); // serves index.html automatically
    app.UseStaticFiles();
    app.UseAuthorization();
    app.MapControllers();

    Log.Information("╔══════════════════════════════════════════════╗");
    Log.Information("║  ITSM·Pro Backend  ·  ASP.NET Core 10        ║");
    Log.Information("║  Dapper + SQLite  ·  http://localhost:5051    ║");
    Log.Information("╚══════════════════════════════════════════════╝");
    Log.Information("Log-Pfade konfiguriert:");
    Log.Information("  App 1: C:\\Users\\Manuel\\Desktop\\FH_Master\\Masterarbeit\\Implementierung\\masterarbeit_mini_apps\\masterarbeit_mini_apps\\release_hub\\logs\\app.log");
    Log.Information("  App 2: C:\\Users\\Manuel\\Desktop\\FH_Master\\Masterarbeit\\Implementierung\\masterarbeit_mini_apps\\masterarbeit_mini_apps\\ops_center\\logs\\app.log");

    app.Run("http://0.0.0.0:5051");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Anwendung konnte nicht gestartet werden");
}
finally
{
    Log.CloseAndFlush();
}
