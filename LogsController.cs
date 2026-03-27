using ITSMPro.Models;
using ITSMPro.Services;
using Microsoft.AspNetCore.Mvc;

namespace ITSMPro.Controllers;

[ApiController]
[Route("api/logs")]
public class LogsController : ControllerBase
{
    private readonly LogParserService _parser;

    public LogsController(LogParserService parser) => _parser = parser;

    // POST /api/logs/parse
    // Body: { "log_file": "C:\\Users\\...\\app.log", "since_line": 0 }
    [HttpPost("parse")]
    public IActionResult Parse([FromBody] LogParseRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.LogFile))
            return BadRequest(new { error = "log_file fehlt" });

        try
        {
            var result = _parser.Parse(req);
            return Ok(result);
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // GET /api/logs/stats?log_file=...
    [HttpGet("stats")]
    public IActionResult Stats([FromQuery] string? log_file)
        => Ok(_parser.GetStats(log_file));

    // GET /api/logs/recent?log_file=...&limit=100
    [HttpGet("recent")]
    public IActionResult Recent(
        [FromQuery] string? log_file,
        [FromQuery] int     limit = 100)
        => Ok(_parser.GetRecent(log_file, limit));
}
