using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Paynles.Services;
using SmartSaverWeb.DataModels;
using SmartSaverWeb.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Concurrent;
using System.Linq;
// Alias: link the EF model to a shorter name
using DbTrackedProduct = SmartSaverWeb.DataModels.TrackedProduct;
namespace SmartSaverWeb.Controllers
{
    // Define the structure of the incoming request from the extension
    public class ProductScanRequest
    {
        public string Email { get; set; }
        public string PrimaryAsin { get; set; }
        public List<string> AdditionalAsins { get; set; }
    }

    // NEW request model for a single product
    public class PrimaryProductLogRequest
    {
        public string Email { get; set; }
        public string Asin { get; set; }
        public string Title { get; set; }
        public decimal? PriceSeen { get; set; }
        public string ImageUrl { get; set; }
        public string RecordType { get; set; } = "Primary"; // ✅ new field
    }

    public class TrackRelatedRequest
    {
        public string RecordType { get; set; }
        public string Email { get; set; }
        public string UserId { get; set; }
        public string? PrimaryAsin { get; set; }
        public List<ProductItem> Items { get; set; }
    }

    public class ProductItem
    {
        public string Asin { get; set; } = "";
        public string Title { get; set; } = "";
        public decimal? PriceSeen { get; set; }
        public string ImageUrl { get; set; } = "";
        public string Url { get; set; } = "";
        public string RecordType { get; set; } = "";
        public string? PrimaryAsin { get; set; }
        public string Source { get; set; } = "";
    }
    // Replace the local TrackedProduct class with this one
    public class TrackedProduct
    {
        public string Id { get; set; } = "";

        public string Email { get; set; } = "";
        public string UserId { get; set; } = "";
        public string Asin { get; set; } = "";
        public string Title { get; set; } = "";
        public decimal? PriceSeen { get; set; }
        public string ImageUrl { get; set; } = "";
        public string Url { get; set; } = "";
        public string RecordType { get; set; } = "";
        public string? PrimaryAsin { get; set; } = "";
        public string Source { get; set; } = "";

        // Group for batch association (you can set one value across a batch)
        public Guid GroupId { get; set; }

        // These two are referenced throughout the file
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string IpAddress { get; set; } = "";
    }

    [ApiController]
    [Route("api/products")]
    public class ProductTrackingController : ControllerBase
    {
        private static readonly ConcurrentDictionary<string, byte> InFlight = new();
        private readonly IWebHostEnvironment _env;

        public ProductTrackingController(IWebHostEnvironment env)
        {
            _env = env ?? throw new ArgumentNullException(nameof(env));
        }

        // NEW Endpoint for logging just the primary product
        [HttpPost("log")]
        public async Task<IActionResult> LogPrimaryProduct(
    [FromBody] PrimaryProductLogRequest request,
    [FromServices] paynles_dbContext ctx)

        {
            if (string.IsNullOrEmpty(request?.Asin))
            {
                return BadRequest(new { error = "missing_asin", message = "ASIN is required." });
            }

            try
            {
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                // Save directly to database (JSON file logging removed)
                var user = await ctx.Users
                    .Where(u => u.Email == request.Email && u.IsActive)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    Console.WriteLine($"⚠️ Active user not found for {request.Email}. Returning error to extension.");
                    // Return explicit error structure for Chrome extension to handle
                    return NotFound(new { error = "user_not_found", message = $"Active user not found for {request.Email}" });
                }

                var marketplace = await ctx.Marketplaces.FirstOrDefaultAsync();
                if (marketplace == null)
                {
                    Console.WriteLine("⚠️ No Marketplace found. Skipping DB insert.");
                    return StatusCode(500, new { error = "marketplace_missing", message = "No Marketplace configured." });
                }

                var recordType = string.IsNullOrWhiteSpace(request.RecordType) ? "Primary" : request.RecordType;
                var deDupWindow = DateTime.UtcNow.AddMinutes(-2);

                // 1) Recent duplicate check (DB)
                var exists = await ctx.TrackedProducts.AnyAsync(tp =>
                    tp.UserId == user.UserId &&
                    tp.MarketplaceId == marketplace.MarketplaceId &&
                    tp.MarketplaceProductCode == request.Asin &&
                    tp.RecordType == recordType &&
                    tp.TimestampUtc >= deDupWindow);

                if (exists)
                {
                    Console.WriteLine($"ℹ️ Suppressed recent duplicate for {request.Asin} [{recordType}]");
                    return Ok(new { suppressed = true, message = "Duplicate ignored (recent)." });
                }

                // 2) In-flight suppression (per-process)
                var key = $"{user.UserId:N}:{marketplace.MarketplaceId:N}:{request.Asin}:{recordType}";
                if (!InFlight.TryAdd(key, 0))
                {
                    return Ok(new { suppressed = true, message = "Duplicate in-flight request suppressed." });
                }

                try
                {
                    var dbRecord = new DbTrackedProduct
                    {
                        ProductId = Guid.NewGuid(),
                        UserId = user.UserId,
                        MarketplaceId = marketplace.MarketplaceId,
                        MarketplaceProductCode = request.Asin,
                        MarketplaceIdType = "ASIN",
                        Title = request.Title,
                        PriceSeen = request.PriceSeen,
                        ImageUrl = request.ImageUrl,
                        RecordType = recordType,
                        TimestampUtc = DateTime.UtcNow,
                        Source = "ChromeExtension",
                        IsActive = true,
                        IpAddress = clientIp
                    };

                    ctx.TrackedProducts.Add(dbRecord);
                    await ctx.SaveChangesAsync();

                    Console.WriteLine($"✅ DB insert OK for ASIN: {request.Asin}, User: {user.Email}");
                    return Ok(new { message = $"Logged ASIN: {request.Asin} to database." });
                }
                finally
                {
                    InFlight.TryRemove(key, out _);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ DB insert error: {ex.Message}");
                return StatusCode(500, new { error = "db_error", message = ex.Message });
            }
        }

        [HttpDelete("{asin}")]
        public IActionResult DeleteProduct(string asin, [FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(asin) || string.IsNullOrWhiteSpace(email))
                return BadRequest("Missing asin or email");

            string path;
            var home = Environment.GetEnvironmentVariable("HOME");

            if (!string.IsNullOrEmpty(home))
            {
                path = Path.Combine(home, "data", "FilesUploaded", "TrackedProducts.json");
            }
            else
            {
                path = Path.Combine("C:\\Users\\Hershey\\Documents\\PaynLes\\FilesUploaded", "TrackedProducts.json");
            }


            if (!System.IO.File.Exists(path))
                return NotFound("Data file not found");

            var json = System.IO.File.ReadAllText(path);
            var list = JsonSerializer.Deserialize<List<Product>>(json) ?? new();

            var updatedList = list
                .Where(p => !(string.Equals(p.Asin, asin, StringComparison.OrdinalIgnoreCase) &&
                              string.Equals(p.Email, email, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            System.IO.File.WriteAllText(path, JsonSerializer.Serialize(updatedList, new JsonSerializerOptions { WriteIndented = true }));
            return Ok();
        }

        [HttpPost("scan")]
        public async Task<IActionResult> ScanAndLogProducts([FromBody] ProductScanRequest request, [FromServices] paynles_dbContext ctx)
        {
            if (string.IsNullOrEmpty(request?.PrimaryAsin))
            {
                return BadRequest("Primary ASIN is required.");
            }

            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            string logFilePath;
            var home = Environment.GetEnvironmentVariable("HOME");

            if (!string.IsNullOrEmpty(home))
            {
                logFilePath = Path.Combine(home, "data", "FilesUploaded", "TrackedProducts.json");
            }
            else
            {
                logFilePath = Path.Combine("C:\\Users\\Hershey\\Documents\\PaynLes\\FilesUploaded", "TrackedProducts.json");
            }

            // Read existing records from the JSON file
            List<TrackedProduct> allProducts;
            if (System.IO.File.Exists(logFilePath))
            {
                var json = await System.IO.File.ReadAllTextAsync(logFilePath);
                allProducts = JsonSerializer.Deserialize<List<TrackedProduct>>(json) ?? new List<TrackedProduct>();
            }
            else
            {
                allProducts = new List<TrackedProduct>();
            }

            bool IsRecentDuplicate(string email, string asin, string recordType, string primaryAsin)
            {
                var since = DateTime.UtcNow.AddMinutes(-2);
                return allProducts.Any(p =>
                    string.Equals(p.Email, email, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.Asin, asin, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.RecordType, recordType, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.PrimaryAsin ?? "", primaryAsin ?? "", StringComparison.OrdinalIgnoreCase) &&
                    p.TimestampUtc >= since);
            }

            var added = 0;

            // Create a record for the primary ASIN (skip if recent duplicate)
            if (!IsRecentDuplicate(request.Email, request.PrimaryAsin, "Primary", null))
            {
                var primaryRecord = new TrackedProduct
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Email = request.Email,
                    TimestampUtc = DateTime.UtcNow,
                    IpAddress = clientIp,
                    Asin = request.PrimaryAsin,
                    RecordType = "Primary",
                    PrimaryAsin = null // It is its own primary
                };
                allProducts.Add(primaryRecord);
                added++;
            }
            else
            {
                Console.WriteLine($"ℹ️ Suppressed recent duplicate (JSON) for primary {request.PrimaryAsin}");
            }

            // Create records for all additional ASINs (dedupe input and recent JSON)
            if (request.AdditionalAsins != null)
            {
                foreach (var additionalAsin in request.AdditionalAsins
                             .Where(a => !string.IsNullOrWhiteSpace(a))
                             .Select(a => a.Trim())
                             .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (IsRecentDuplicate(request.Email, additionalAsin, "Additional", request.PrimaryAsin))
                    {
                        Console.WriteLine($"ℹ️ Suppressed recent duplicate (JSON) for related {additionalAsin}");
                        continue;
                    }

                    var additionalRecord = new TrackedProduct
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Email = request.Email,
                        TimestampUtc = DateTime.UtcNow,
                        IpAddress = clientIp,
                        Asin = additionalAsin,
                        RecordType = "Additional",
                        PrimaryAsin = request.PrimaryAsin // Link to the main item
                    };
                    allProducts.Add(additionalRecord);
                    added++;
                }
            }

            // Write the updated list back to the file
            var updatedJson = JsonSerializer.Serialize(allProducts, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(logFilePath, updatedJson);

            // ✅ Step: Also insert into SQL (temporary dual logging)
            try
            {
                var user = await ctx.Users
                    .Where(u => u.Email == request.Email && u.IsActive)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    Console.WriteLine($"⚠️ User {request.Email} not active or missing. Skipping DB insert.");
                }
                else
                {
                    var marketplace = await ctx.Marketplaces.FirstOrDefaultAsync();
                    if (marketplace == null)
                    {
                        Console.WriteLine("⚠️ No Marketplace found. Skipping DB insert.");
                    }
                    else
                    {
                        var allDbRecords = new List<DbTrackedProduct>();

                        // 🚫 Do NOT insert the primary again here.
                        // We only insert related products to avoid duplicating the primary ASIN.

                        if (request.AdditionalAsins != null)
                        {
                            // De-duplicate additional ASINs just in case
                            foreach (var asin in request.AdditionalAsins
                                         .Where(a => !string.IsNullOrWhiteSpace(a))
                                         .Select(a => a.Trim())
                                         .Distinct(StringComparer.OrdinalIgnoreCase))
                            {
                                allDbRecords.Add(new DbTrackedProduct
                                {
                                    ProductId = Guid.NewGuid(),
                                    UserId = user.UserId,
                                    MarketplaceId = marketplace.MarketplaceId,
                                    MarketplaceProductCode = asin,
                                    MarketplaceIdType = "ASIN",
                                    Title = null,
                                    PriceSeen = null,
                                    ImageUrl = null,
                                    RecordType = "RelatedProduct",
                                    TimestampUtc = DateTime.UtcNow,
                                    Source = "ChromeExtension-Related",
                                    IsActive = true,
                                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                                    ParentProductId = request.PrimaryAsin
                                });
                            }
                        }

                        if (allDbRecords.Count > 0)
                        {
                            await ctx.TrackedProducts.AddRangeAsync(allDbRecords);
                            await ctx.SaveChangesAsync();
                            Console.WriteLine($"✅ Inserted {allDbRecords.Count} related records to DB for user {user.Email}");
                        }
                        else
                        {
                            Console.WriteLine("ℹ️ No related ASINs to insert.");
                        }
                    }
                }
            }
            catch (Exception dbEx)
            {
                Console.WriteLine($"⚠️ DB insert failed (TrackRelated): {dbEx.Message}");
            }

            return Ok(new { Message = $"Logged {added} new product(s)." });
        }
        public class BulkProductLogRequest
        {
            public string Mode { get; set; }
            public string RecordType { get; set; }
            public string Datetime { get; set; }
            public string Email { get; set; }
            public string IpAddress { get; set; }
            public List<BulkItem> Items { get; set; }
        }

        public class BulkItem
        {
            public string Asin { get; set; }
            public string Title { get; set; }
            public double? Price { get; set; }
            public string Image { get; set; }

           public string PrimaryAsin { get; set; }
            [JsonPropertyName("recordType")]
            
            public string RecordType { get; set; }


            public string Source { get; set; }


            public string? Url { get; set; }  // ✅ nullable = optional field
        }

        public class HtmlUploadRequest
        {
            public string Email { get; set; }
            public string Html { get; set; }
        }
        [HttpPost("upload-html")]
        public async Task<IActionResult> UploadHtml([FromBody] HtmlUploadRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Html) || string.IsNullOrWhiteSpace(request.Email))
                return BadRequest("Missing html or email");

            var inboxPath = Path.Combine(_env.ContentRootPath, "wwwroot", "App_Data", "HtmlInbox");
            Directory.CreateDirectory(inboxPath);
            // Clean email (remove special characters that can't be in filenames)
            var safeEmail = request.Email.Replace("@", "_at_").Replace(".", "_");

            // Use UTC timestamp to avoid collisions
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

            var filename = $"{safeEmail}_{timestamp}.html";

            var fullPath = Path.Combine(inboxPath, filename);

            await System.IO.File.WriteAllTextAsync(fullPath, request.Html);

            return Ok(new { message = "HTML uploaded", filename });
        }

        [HttpPost("track-related")]
        public async Task<IActionResult> TrackRelatedProducts([FromBody] TrackRelatedRequest payload)
        {
            // ✅ Dump JSON to console (formatted)
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(payload, options);
            Console.WriteLine("📦 Received TrackRelatedRequest:\n" + json);

            // save to DB...
            return Ok(new { success = true });
        }




        [HttpGet("db-test")]
        public IActionResult TestDb([FromServices] paynles_dbContext     ctx)
        {
            var tableCount = ctx.Users.Count();
            return Ok(new { success = true, users = tableCount });
        }
     
        [HttpPost("import-json")]
        [HttpGet("import-json")] // TEMPORARY for testing in browser
        public async Task<IActionResult> ImportJson([FromQuery] string email,
    [FromServices] Paynles.Services.ProductFileImporter importer,
    [FromServices] paynles_dbContext ctx)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Email is required.");

            var count = await importer.ImportAndRemoveAsync(email, ctx);
            return Ok(new { success = true, imported = count, email });
        }




        [HttpPost("add-relateditems-to-my-list")]
        public async Task<IActionResult> AddRelatedProducts(
            [FromBody] BulkProductLogRequest request,
            [FromServices] paynles_dbContext ctx)
        {
            Console.WriteLine("🚀 /api/products/add-relateditems-to-my-list (DB only) called");

            if (request == null || request.Items == null || !request.Items.Any())
                return BadRequest(new { error = "invalid_payload" });

            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { error = "missing_email" });

            var primary = request.Items.FirstOrDefault(i =>
                string.Equals(i.RecordType, "PrimaryProduct", StringComparison.OrdinalIgnoreCase));

            if (primary == null || string.IsNullOrWhiteSpace(primary.Asin))
                return BadRequest(new { error = "missing_primary_product_in_payload" });

            string? primaryAsin = primary.Asin.Trim();

            try
            {
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                var batchGroupId = Guid.NewGuid();
                var since = DateTime.UtcNow.AddMinutes(-2);

                var user = await ctx.Users
                    .Where(u => u.Email == request.Email && u.IsActive)
                    .FirstOrDefaultAsync();

                var marketplace = await ctx.Marketplaces.FirstOrDefaultAsync();

                if (user == null || marketplace == null)
                    return BadRequest(new { error = "user_or_marketplace_not_found" });

                var allItems = request.Items
                    .Where(i =>
                        (string.Equals(i.RecordType, "RelatedProduct", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(i.RecordType, "PrimaryProduct", StringComparison.OrdinalIgnoreCase)) &&
                        !string.IsNullOrWhiteSpace(i.Asin) &&
                        !string.IsNullOrWhiteSpace(i.Title))
                    .Select(i => new BulkItem
                    {
                        Asin = i.Asin.Trim(),
                        Title = i.Title.Trim(),
                        Price = i.Price,
                        Image = i.Image,
                        Source = i.Source,
                        Url = i.Url,
                        RecordType = i.RecordType,
                        PrimaryAsin = i.PrimaryAsin
                    })
                    .DistinctBy(i => i.Asin.ToUpperInvariant())
                    .ToList();


                static string? Trunc(string? s, int max) =>
                    string.IsNullOrEmpty(s) ? s : (s.Length > max ? s[..max] : s);

                var dbInserts = new List<DbTrackedProduct>();
                int skipped = 0, addedDb = 0;
                string? ip = Trunc(clientIp, 50);
                                string? parent = Trunc(primaryAsin, 100);

                foreach (var item in allItems)
                {
                    string recordType = string.IsNullOrWhiteSpace(item.RecordType)
                        ? "RelatedProduct"
                        : item.RecordType;

                    string source = string.IsNullOrWhiteSpace(item.Source)
                        ? "ChromeExtension-Related"
                        : Trunc(item.Source, 100);

                    bool isDuplicate = await ctx.TrackedProducts.AnyAsync(tp =>
                        tp.UserId == user.UserId &&
                        tp.MarketplaceId == marketplace.MarketplaceId &&
                        tp.MarketplaceProductCode == item.Asin &&
                        tp.RecordType == recordType &&
                        tp.TimestampUtc >= since);

                    if (isDuplicate)
                    {
                        Console.WriteLine($"ℹ️ DB duplicate suppressed for ASIN {item.Asin}");
                        skipped++;
                        continue;
                    }

                    dbInserts.Add(new DbTrackedProduct
                    {
                        ProductId = Guid.NewGuid(),
                        UserId = user.UserId,
                        MarketplaceId = marketplace.MarketplaceId,
                        MarketplaceProductCode = Trunc(item.Asin, 100)!,
                        MarketplaceIdType = "ASIN",
                        Title = Trunc(item.Title, 500),
                        PriceSeen = item.Price.HasValue ? (decimal?)item.Price.Value : null,
                        ImageUrl = Trunc(item.Image, 1000),
                        RecordType = recordType,
                        TimestampUtc = DateTime.UtcNow,
                        Source = source,
                        IpAddress = ip,
                        IsActive = true,
                        ParentProductId = string.Equals(recordType, "PrimaryProduct", StringComparison.OrdinalIgnoreCase)
                            ? null
                            : parent,
                        GroupId = batchGroupId.ToString("N")
                    });
                }



                if (dbInserts.Count > 0)
                {
                    await ctx.TrackedProducts.AddRangeAsync(dbInserts);
                    addedDb = await ctx.SaveChangesAsync();
                }

                var message = $"Related DB inserted: {addedDb}. Skipped: {skipped}.";
                Console.WriteLine("📦 " + message);

                return Ok(new
                {
                    success = true,
                    message,
                    groupId = batchGroupId,
                    dbAdded = addedDb,
                    skipped
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ ERROR during AddRelatedProducts: " + ex.Message);
                return StatusCode(500, new { message = "Server error", detail = ex.Message });
            }
        }


        // this adds a single item to you account
        [HttpPost("add-to-my-list")]

        public async Task<IActionResult> AddSingleProducts(
    [FromBody] BulkProductLogRequest request,
    [FromServices] paynles_dbContext ctx)
        {
            Console.WriteLine("🚀 /api/products/add-to-my-list (DB only) called");

            if (request == null)
                return BadRequest(new { error = "invalid_payload" });

            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { error = "missing_email" });

            try
            {
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                var batchGroupId = Guid.NewGuid();
                var since = DateTime.UtcNow.AddMinutes(-2);

                // Load user + marketplace for DB inserts
                var user = await ctx.Users
                    .Where(u => u.Email == request.Email && u.IsActive)
                    .FirstOrDefaultAsync();

                var marketplace = await ctx.Marketplaces.FirstOrDefaultAsync();

                if (user == null || marketplace == null)
                {
                    return BadRequest(new { error = "user_or_marketplace_not_found" });
                }

                var items = (request.Items ?? new List<BulkItem>())
                    .Where(i => !string.IsNullOrWhiteSpace(i.Asin) && !string.IsNullOrWhiteSpace(i.Title))
                    .Select(i => new BulkItem
                    {
                        Asin = i.Asin.Trim(),
                        Title = i.Title.Trim(),
                        Price = i.Price,
                        Image = i.Image, 
                        RecordType = i.RecordType
                    })
                    .DistinctBy(i => i.Asin.ToUpperInvariant())
                    .ToList();

                static string? Trunc(string? s, int max) =>
                    string.IsNullOrEmpty(s) ? s : (s.Length > max ? s[..max] : s);

                var dbInserts = new List<DbTrackedProduct>();
                int skipped = 0, addedDb = 0;
                   string? ip = Trunc(clientIp, 50);

                foreach (var item in items)
                {
                    string recordType = string.IsNullOrWhiteSpace(item.RecordType) ? "OrderHistory" : (item.RecordType.Length > 50 ? item.RecordType[..50] : item.RecordType);
                    string source = string.IsNullOrWhiteSpace(item.Source) ? "ChromeExtension" : (item.Source.Length > 100 ? item.Source[..100] : item.Source);
                    string? parent = string.IsNullOrWhiteSpace(item.PrimaryAsin) ? null : (item.PrimaryAsin.Length > 100 ? item.PrimaryAsin[..100] : item.PrimaryAsin);

                    bool isDuplicate = await ctx.TrackedProducts.AnyAsync(tp =>
                        tp.UserId == user.UserId &&
                        tp.MarketplaceId == marketplace.MarketplaceId &&
                        tp.MarketplaceProductCode == item.Asin &&
                        tp.RecordType == recordType &&
                        tp.TimestampUtc >= since);

                    if (isDuplicate)
                    {
                        Console.WriteLine($"ℹ️ DB duplicate suppressed for ASIN {item.Asin}");
                        skipped++;
                        continue;
                    }

                    dbInserts.Add(new DbTrackedProduct
                    {
                        ProductId = Guid.NewGuid(),
                        UserId = user.UserId,
                        MarketplaceId = marketplace.MarketplaceId,
                        MarketplaceProductCode = Trunc(item.Asin, 100)!,
                        MarketplaceIdType = "ASIN",
                        Title = Trunc(item.Title, 500),
                        PriceSeen = item.Price.HasValue ? (decimal?)item.Price.Value : null,
                        ImageUrl = Trunc(item.Image, 1000),
                        RecordType = recordType,
                        TimestampUtc = DateTime.UtcNow,
                        Source = source,
                        IpAddress = ip,
                        IsActive = true,
                        ParentProductId = parent,
                        GroupId = batchGroupId.ToString("N")
                    });

                }

                if (dbInserts.Count > 0)
                {
                    await ctx.TrackedProducts.AddRangeAsync(dbInserts);
                    addedDb = await ctx.SaveChangesAsync();
                    // ===================================================
                    // BEGIN INSERT: Create default notification rules
                    // ===================================================
                    try
                    {
                        foreach (var product in dbInserts)
                        {
                            decimal savedPrice = product.PriceSeen ?? 0m; // baseline
                            decimal dropPercent = 10m;                    // default rule
                            decimal notificationValue = savedPrice * (1 - (dropPercent / 100));

                            var rule = new ProductNotification
                            {
                                NotificationId = Guid.NewGuid(),
                                ProductId = product.ProductId,
                                UserId = product.UserId,
                                RuleType = "percent-drop",
                                DropType = "percent",
                                DropValue = dropPercent,
                                SavedPrice = savedPrice,
                                NotificationValue = notificationValue,
                                DateSet = DateTime.UtcNow,
                                IsActive = true
                            };

                            await ctx.ProductNotifications.AddAsync(rule);
                        }

                        await ctx.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("⚠️ Failed to create notification rule: " + ex.Message);
                        // Optional: log to DB or external logger
                    }
                    // ===================================================
                    // END INSERT: Create default notification rules
                    // ===================================================

                }

                var message = $"DB inserted: {addedDb}. Skipped: {skipped}.";
                Console.WriteLine("📦 " + message);

                return Ok(new
                {
                    success = true,
                    message,
                    groupId = batchGroupId,
                    dbAdded = addedDb,
                    skipped
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ ERROR during AddMultipleProducts: " + ex.Message);
                return StatusCode(500, new { message = "Server error", detail = ex.Message });
            }
        }

        [HttpGet("read-json")]
        public async Task<IActionResult> ReadJson([FromQuery] string email, [FromServices] Paynles.Services.ProductFileImporter importer)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Email is required.");

            var items = await importer.ReadProductsByEmailAsync(email);

            // Return a lightweight preview to keep response small
            var preview = items.Select(p => new {
                p.Asin,
                p.Title,
                p.PriceSeen,
                p.RecordType,
                p.TimestampUtc,
                p.ImageUrl
            }).Take(10);

            return Ok(new
            {
                success = true,
                email,
                count = items.Count,
                preview
            });
        }

        [HttpPost("import-by-email")]
        [HttpGet("import-by-email")]
        public async Task<IActionResult> ImportByEmail([FromQuery] string email, [FromServices] ProductFileImporter importer)
        
        
        
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Email is required.");

            var count = await importer.ImportByEmailAsync(email);

            return Ok(new
            {
                success = true,
                imported = count,
                email
            });
        }


    }
}