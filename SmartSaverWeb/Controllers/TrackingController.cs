using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace YourNamespace.Controllers
{
    [ApiController]
    [Route("api/log")]
    public class TrackingController : ControllerBase
    {
        public class TrackRequest
        {
            public string UserId { get; set; }           // kept for backward compat
            public string Asin { get; set; }
            public string Url { get; set; }
            public string Title { get; set; }

            // NEW (optional)
            public string Email { get; set; }            // may be null -> "No Email" on client
            public decimal? PriceSeen { get; set; }      // price the user saw at the time
            public string Source { get; set; }           // e.g. "track" | "flag" (optional)
        }

        [HttpPost]
        public async Task<IActionResult> LogClick([FromBody] TrackRequest request)
        {
            if (request == null) return BadRequest();

            // Prefer proxy header if present (Azure App Service / Front Door etc.)
            var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            var clientIp = !string.IsNullOrWhiteSpace(forwardedFor)
                ? forwardedFor.Split(',')[0].Trim()
                : HttpContext.Connection.RemoteIpAddress?.ToString();

            // Minimal CSV escaping
            static string Q(object v)
            {
                var s = v?.ToString() ?? "";
                s = s.Replace("\"", "\"\"");
                return $"\"{s}\"";
            }

            var when = DateTime.UtcNow.ToString("u");
            var line = string.Join(",",
                Q(when),
                Q(clientIp),
                Q(request.UserId),
                Q(request.Email),                   // may be null/blank
                Q(request.Asin),
                Q(request.PriceSeen?.ToString("0.00")),
                Q(request.Source),
                Q(request.Title),
                Q(request.Url)
            );

            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FilesUploaded", "TrackedLogs.txt");

            // Create file with header once
            if (!System.IO.File.Exists(logPath))
            {
                var header = "\"WhenUtc\",\"ClientIp\",\"UserId\",\"Email\",\"ASIN\",\"PriceSeen\",\"Source\",\"Title\",\"Url\"";
                await System.IO.File.WriteAllTextAsync(logPath, header + Environment.NewLine);
            }

            await System.IO.File.AppendAllTextAsync(logPath, line + Environment.NewLine);
            return Ok();
        }
    }
}
