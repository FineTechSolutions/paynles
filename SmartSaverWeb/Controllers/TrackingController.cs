using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartSaverWeb.DataModels;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Concurrent; // <-- add this

namespace YourNamespace.Controllers
{
    [ApiController]
    [Route("api/log")]
    public class TrackingController : ControllerBase
    {
        // In-flight suppression (per-process). Keyed by user+marketplace+asin+recordType.
        private static readonly ConcurrentDictionary<string, byte> InFlight = new();

        public class TrackRequest
        {
            public string UserId { get; set; }
            public string Asin { get; set; }
            public string Url { get; set; }
            public string Title { get; set; }
            public string Email { get; set; }
            public decimal? PriceSeen { get; set; }
            public string Source { get; set; }       // e.g. "Click", "ChromeExtension-Text"
            public string ImageUrl { get; set; }     // optional
        }

        [HttpPost]
        public async Task<IActionResult> LogClick([FromBody] TrackRequest request, [FromServices] paynles_dbContext ctx)
        {
            // ---- TEMP LOGGING: headers + payload ----
            var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            var clientIp = !string.IsNullOrWhiteSpace(forwardedFor)
                ? forwardedFor.Split(',')[0].Trim()
                : HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            var headerSnapshot = new
            {
                Path = Request.Path.ToString(),
                Query = Request.QueryString.ToString(),
                ClientIp = clientIp,
                XForwardedFor = forwardedFor,
                UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
                Origin = Request.Headers["Origin"].FirstOrDefault(),
                Referer = Request.Headers["Referer"].FirstOrDefault()
            };

            Console.WriteLine("[/api/log] headers: " + JsonSerializer.Serialize(headerSnapshot));
            Console.WriteLine("[/api/log] payload: " + JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            }));

            if (request == null)
                return BadRequest(new { error = "bad_request", message = "Request body is required." });

            if (string.IsNullOrWhiteSpace(request.Asin))
                return BadRequest(new { error = "missing_asin", message = "ASIN is required." });

            try
            {
                // Resolve user (prefer Email, fallback to GUID UserId)
                User user = null;
                if (!string.IsNullOrWhiteSpace(request.Email))
                {
                    user = await ctx.Users.FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);
                }
                else if (Guid.TryParse(request.UserId, out var userGuid))
                {
                    user = await ctx.Users.FirstOrDefaultAsync(u => u.UserId == userGuid && u.IsActive);
                }

                if (user == null)
                    return NotFound(new { error = "user_not_found", message = "Active user not found by Email or UserId." });

                var marketplace = await ctx.Marketplaces.FirstOrDefaultAsync();
                if (marketplace == null)
                    return StatusCode(500, new { error = "marketplace_missing", message = "No Marketplace configured." });

                // Intent: map green “+ Track this” (ChromeExtension-Text) to Primary; else Click
                var isPrimaryIntent =
                    string.Equals(request.Source, "ChromeExtension-Text", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Request.Query["mode"], "primary", StringComparison.OrdinalIgnoreCase);

                var normalizedTitle = string.IsNullOrWhiteSpace(request.Title) || request.Title == "(no title)" ? null : request.Title;
                var recordType = isPrimaryIntent ? "Primary" : "Click";
                var source = string.IsNullOrWhiteSpace(request.Source)
                    ? (isPrimaryIntent ? "ChromeExtension" : "ChromeExtension-Log")
                    : request.Source;

                // In-flight suppression key (no DB lookups)
                var key = $"{user.UserId:N}:{marketplace.MarketplaceId:N}:{request.Asin}:{recordType}";
                if (!InFlight.TryAdd(key, 0))
                {
                    // Another identical request is currently being processed on this instance.
                    return Ok(new { success = true, suppressed = true, message = "Duplicate in-flight request suppressed." });
                }

                try
                {
                    var dbRecord = new TrackedProduct
                    {
                        ProductId = Guid.NewGuid(),
                        UserId = user.UserId,
                        MarketplaceId = marketplace.MarketplaceId,
                        MarketplaceProductCode = request.Asin,
                        MarketplaceIdType = "ASIN",
                        Title = normalizedTitle,
                        PriceSeen = request.PriceSeen,
                        ImageUrl = isPrimaryIntent ? request.ImageUrl : null, // image only for primaries
                        RecordType = recordType,
                        TimestampUtc = DateTime.UtcNow,
                        Source = source,
                        IsActive = true,
                        IpAddress = clientIp,
                        ParentProductId = null
                    };

                    ctx.TrackedProducts.Add(dbRecord);
                    await ctx.SaveChangesAsync();

                    return Ok(new { success = true, asPrimary = isPrimaryIntent, message = isPrimaryIntent ? "Primary saved." : "Click logged." });
                }
                finally
                {
                    InFlight.TryRemove(key, out _);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[/api/log] error: " + ex.Message);
                return StatusCode(500, new { error = "db_error", message = ex.Message });
            }
        }
    }
}
