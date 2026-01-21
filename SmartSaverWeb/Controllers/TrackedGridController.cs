using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartSaverWeb.DataModels;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace SmartSaverWeb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrackedGridController : ControllerBase
    {
        private readonly paynles_dbContext _db;

        public TrackedGridController(paynles_dbContext db)
        {
            _db = db;
        }
        public class GroupDto
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = "";

            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            [JsonPropertyName("parentId")]
            public string? ParentId { get; set; }

            [JsonPropertyName("icon")]
            public string? Icon { get; set; }

            [JsonPropertyName("collectionType")]
            public string? CollectionType { get; set; } = "manual";
        }
        // === PLACEHOLDER ===
        // We'll move endpoints in next steps.
        // GET /api/groups?email=someone@example.com
        [HttpGet("groups")]
        public async Task<ActionResult<IEnumerable<GroupDto>>> GetGroups([FromQuery] string? email)
        {
            var query = _db.TrackedProducts
                .Include(p => p.User)
                .Where(p => p.IsActive && !p.ToDelete && p.GroupId != null && p.GroupId != "");

            if (!string.IsNullOrWhiteSpace(email))
            {
                query = query.Where(p => p.User.Email == email && p.User.IsActive);
            }

            var groupIds = await query
                .Select(p => p.GroupId)
                .Distinct()
                .OrderBy(g => g)
                .ToListAsync();

            var groups = groupIds
                .Select(id => new GroupDto
                {
                    Id = id!,
                    Name = id!,
                    ParentId = null,
                    Icon = null,
                    CollectionType = "manual"
                })
                .ToList();

            return Ok(groups);
        }
        public class ProductDto
        {
            [JsonPropertyName("asin")]
            public string Asin { get; set; } = "";

            [JsonPropertyName("title")]
            public string Title { get; set; } = "";

            [JsonPropertyName("price")]
            public decimal Price { get; set; }

            [JsonPropertyName("alertBelow")]
            public decimal AlertBelow { get; set; }

            [JsonPropertyName("imageUrl")]
            public string? ImageUrl { get; set; }

            [JsonPropertyName("tags")]
            public List<string> Tags { get; set; } = new();

            [JsonPropertyName("groupId")]
            public string? GroupId { get; set; }

            [JsonPropertyName("alertStatus")]
            public string AlertStatus { get; set; } = "normal";

            // When the current price was last observed (TrackedProducts.TimestampUtc)
            [JsonPropertyName("lastSeenUtc")]
            public DateTime? LastSeenUtc { get; set; }
            // Unique tracked product row (used by UI actions like keep/dispose later)
            [JsonPropertyName("productId")]
            public Guid ProductId { get; set; }

            // Current price snapshot from TrackedProducts.PriceSeen
            [JsonPropertyName("currentPrice")]
            public decimal CurrentPrice { get; set; }

            // Saved price captured when alert rule was created (ProductNotifications.SavedPrice)
            [JsonPropertyName("savedPrice")]
            public decimal? SavedPrice { get; set; }

            // Alert rule mode: "percent_drop" or "fixed" (derived from DropType)
            [JsonPropertyName("alertMode")]
            public string? AlertMode { get; set; }

        }// Unique tracked product row (used by UI actions like keep/dispose later)
        [JsonPropertyName("productId")]
public Guid ProductId { get; set; }

// Current price snapshot from TrackedProducts.PriceSeen
[JsonPropertyName("currentPrice")]
public decimal CurrentPrice { get; set; }

// Saved price captured when alert rule was created (ProductNotifications.SavedPrice)
[JsonPropertyName("savedPrice")]
public decimal? SavedPrice { get; set; }

// Alert rule mode: "percent_drop" or "fixed" (derived from DropType)
[JsonPropertyName("alertMode")]
public string? AlertMode { get; set; }

        // --------------------------------------------------------
        // GET /api/products?email=someone@example.com
        // --------------------------------------------------------
        [HttpGet("products")]
        public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts()
        {
            // 1️⃣ Require validated session
            var sessionUserId = HttpContext.Session.GetString("ActiveUserId");
            if (string.IsNullOrEmpty(sessionUserId))
                return Unauthorized("User session not found. Please validate first.");

            if (!Guid.TryParse(sessionUserId, out var userId))
                return Unauthorized("Invalid session user ID.");

            // 2️⃣ Query only that user’s active products
            var query = _db.TrackedProducts
                .Include(p => p.User)
                .Include(p => p.Marketplace)
                .Where(p => p.IsActive && !p.ToDelete && p.UserId == userId && p.User.IsActive);

            // 3️⃣ Project result
            var list = await query
                .OrderByDescending(p => p.TimestampUtc)
              .Select(p => new ProductDto
              {
                  // IDs
                  ProductId = p.ProductId,                                   // UI needs stable key for actions
                  Asin = p.MarketplaceProductCode,                           // ASIN source of truth

                  // Display snapshot
                  Title = p.Title ?? p.MarketplaceProductCode,               // Fallback to code if title missing
                  ImageUrl = p.ImageUrl,                                     // Can be null; UI has fallback image

                  // Pricing
                  CurrentPrice = p.PriceSeen ?? 0m,                          // Current price snapshot
                  Price = p.PriceSeen ?? 0m,                                 // (keep existing field for now)
                  LastSeenUtc = p.TimestampUtc, // TrackedProducts.TimestampUtc                              // When we saw/saved this product

                  // Alert rule (latest active)
                  SavedPrice = p.ProductNotifications
                    .Where(n => n.IsActive)
                    .OrderByDescending(n => n.DateSet)
                    .Select(n => (decimal?)n.SavedPrice)
                    .FirstOrDefault(),                                     // Saved price at rule creation

                              AlertBelow = p.ProductNotifications
                    .Where(n => n.IsActive)
                    .OrderByDescending(n => n.DateSet)
                    .Select(n => n.NotificationValue)
                    .FirstOrDefault(),                                     // Threshold value that triggers alert

                              AlertMode = p.ProductNotifications
                    .Where(n => n.IsActive)
                    .OrderByDescending(n => n.DateSet)
                    .Select(n => n.DropType == "percent" ? "percent_drop" : "fixed")
                    .FirstOrDefault(),                                     // UI-friendly mode label

                  AlertStatus = "normal",                                    // Placeholder until we compute it
                  Tags = new List<string>(),                                 // TODO later
                  GroupId = p.GroupId
              })

                .ToListAsync();

            return Ok(list);
        }

        [HttpGet("spaces")]
        public async Task<IActionResult> GetSpaces()
        {
            // 1️⃣ Require a validated session
            var sessionUserId = HttpContext.Session.GetString("ActiveUserId");
            if (string.IsNullOrEmpty(sessionUserId))
                return Unauthorized("User session not found. Please validate first.");

            if (!Guid.TryParse(sessionUserId, out var userId))
                return Unauthorized("Invalid session user ID.");

            // 2️⃣ Fetch spaces owned by the session user
            var spaces = await _db.Spaces
                .Where(s => s.OwnerUserId == userId && s.IsActive)
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.SpaceName)
                .Select(s => new
                {
                    id = s.SpaceId,
                    name = s.SpaceName,
                    icon = s.Icon,
                    color = s.ColorCode
                })
                .ToListAsync();

            return Ok(spaces);
        }


        // 🔹 Keep selected items (set Keep = 1)
        [HttpPost("products/keep")]
        public async Task<IActionResult> KeepSelected([FromBody] List<Guid> productIds)
        {
            if (productIds == null || productIds.Count == 0)
                return BadRequest("No ProductIds received.");

            var products = await _db.TrackedProducts
                .Where(p => productIds.Contains(p.ProductId))
                .ToListAsync();

            if (!products.Any())
                return NotFound("No matching products found.");

            foreach (var p in products)
                p.Keep = true;

            await _db.SaveChangesAsync();

            return Ok(new { success = true, updated = products.Count });
        }

        // ==========================================
        // POST /api/spaces  → create a new space
        // ==========================================
        [HttpPost("spaces")]
        public async Task<IActionResult> AddSpace([FromBody] SpaceCreateRequest req)
        {
            try
            {
                if (req == null || string.IsNullOrWhiteSpace(req.SpaceName))
                    return BadRequest("SpaceName is required.");

                var space = new Space
                {
                    SpaceId = Guid.NewGuid(),
                    OwnerUserId = req.OwnerUserId ?? Guid.Empty,   // replace Guid.Empty with logged user if available
                    SpaceName = req.SpaceName.Trim(),
                    SpaceType = req.SpaceType ?? "custom",
                    CreatedUtc = DateTime.UtcNow,
                    IsActive = true,
                    SortOrder = 0,
                    Icon = req.Icon ?? "folder",
                    ColorCode = req.ColorCode ?? "#2563eb",
                    Description = req.Description ?? string.Empty
                };

                _db.Spaces.Add(space);

                // try to save and catch DB errors
                await _db.SaveChangesAsync();

                Console.WriteLine($"✅ Space saved successfully: {space.SpaceName} ({space.SpaceId})");

                return Ok(new
                {
                    success = true,
                    id = space.SpaceId,
                    name = space.SpaceName,
                    icon = space.Icon,
                    color = space.ColorCode
                });
            }
            catch (DbUpdateException dbEx)
            {
                // This will capture SQL-level or EF constraint errors
                var msg = dbEx.InnerException?.Message ?? dbEx.Message;
                Console.WriteLine("❌ DbUpdateException while saving Space: " + msg);
                return StatusCode(500, $"Database error while saving Space: {msg}");
            }
            catch (Exception ex)
            {
                // Any other unexpected .NET error
                Console.WriteLine("❌ Exception while saving Space: " + ex);
                return StatusCode(500, $"Unexpected error while saving Space: {ex.Message}");
            }
        }


        public class SpaceCreateRequest
        {
            public Guid? OwnerUserId { get; set; }
            public string? SpaceName { get; set; }
            public string? SpaceType { get; set; }
            public string? Icon { get; set; }
            public string? ColorCode { get; set; }
            public string? Description { get; set; }
        }
        [HttpGet("validate-user")]
        public async Task<IActionResult> ValidateUser([FromQuery] string? email)
        {
            try
            {
                // 1) Reuse existing session (refresh support)
                // 1️⃣ Session reuse / account switching logic
                var sessionUserId = HttpContext.Session.GetString("ActiveUserId");
                var sessionEmail = HttpContext.Session.GetString("ActiveUserEmail");

                // Case A: Refresh (no email in URL) → reuse session
                if (string.IsNullOrWhiteSpace(email) &&
                    !string.IsNullOrEmpty(sessionUserId) &&
                    !string.IsNullOrEmpty(sessionEmail))
                {
                    return Ok(new
                    {
                        success = true,
                        userId = sessionUserId,
                        email = sessionEmail
                    });
                }

                // Case B: Email in URL matches session → reuse session
                if (!string.IsNullOrWhiteSpace(email) &&
                    !string.IsNullOrEmpty(sessionEmail) &&
                    string.Equals(email, sessionEmail, StringComparison.OrdinalIgnoreCase))
                {
                    return Ok(new
                    {
                        success = true,
                        userId = sessionUserId,
                        email = sessionEmail
                    });
                }

                // Otherwise: email is different → fall through and switch account


                // 2) First entry requires email
                if (string.IsNullOrWhiteSpace(email))
                    return BadRequest(new { success = false, error = "Email is required." });

                // 3) DB lookup
                var user = await _db.Users
                    .Where(u => u.Email == email)
                    .Select(u => new { u.UserId, u.IsActive })
                    .FirstOrDefaultAsync();

                if (user == null)
                    return NotFound(new { success = false, error = "Invalid email address." });

                if (!user.IsActive)
                    return Unauthorized(new { success = false, error = "User is not authorized yet." });

                // 4) Establish session
                HttpContext.Session.SetString("ActiveUserId", user.UserId.ToString());
                HttpContext.Session.SetString("ActiveUserEmail", email);

                return Ok(new { success = true, userId = user.UserId, email });
            }
            catch (Exception ex)
            {
                // TEMP diagnostics: shows up in Azure Log Stream
                Console.WriteLine("❌ ValidateUser crashed");
                Console.WriteLine(ex.ToString());

                // TEMP diagnostics: return JSON so React/DevTools can see the real error
                return StatusCode(500, new
                {
                    success = false,
                    error = "ValidateUser failed",
                    type = ex.GetType().Name,
                    message = ex.Message
                });
            }
        }



        // 🔹 Dispose selected items (set ToDelete = 1 and DateMarkedToDelete)
        [HttpPost("products/dispose")]
        public async Task<IActionResult> DisposeSelected([FromBody] List<Guid> productIds)
        {
            if (productIds == null || productIds.Count == 0)
                return BadRequest("No ProductIds received.");

            var products = await _db.TrackedProducts
                .Where(p => productIds.Contains(p.ProductId))
                .ToListAsync();

            if (!products.Any())
                return NotFound("No matching products found.");

            foreach (var p in products)
            {
                p.ToDelete = true;
                p.DateMarkedToDelete = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            return Ok(new { success = true, updated = products.Count });
        }
        // === BACKWARD COMPATIBILITY ALIASES ===

        // /api/groups  → /api/trackedgrid/groups
        [HttpGet("/api/groups")]
        public Task<ActionResult<IEnumerable<GroupDto>>> GetGroupsAlias([FromQuery] string? email)
            => GetGroups(email);

        // /api/products  → /api/trackedgrid/products
        [HttpGet("/api/products")]
        public Task<ActionResult<IEnumerable<ProductDto>>> GetProductsAlias()
          => GetProducts();
        // /api/mytracked/keep  → /api/trackedgrid/products/keep
        [HttpPost("/api/mytracked/keep")]
        public Task<IActionResult> KeepSelectedAlias([FromBody] List<Guid> ids)
            => KeepSelected(ids);

        // /api/mytracked/dispose  → /api/trackedgrid/products/dispose
        [HttpPost("/api/mytracked/dispose")]
        public Task<IActionResult> DisposeSelectedAlias([FromBody] List<Guid> ids)
            => DisposeSelected(ids);

    }
}
