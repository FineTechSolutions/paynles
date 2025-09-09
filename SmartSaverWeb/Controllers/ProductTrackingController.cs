using Microsoft.AspNetCore.Mvc;
using SmartSaverWeb.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
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



    // Define the structure of the record to be saved in the JSON file
    public class TrackedProduct
    {
        public string Id { get; set; } // Unique value for deletion
        public string Email { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string IpAddress { get; set; }
        public string Asin { get; set; }
        public string RecordType { get; set; } // "Primary" or "Additional"
        public string? PrimaryAsin { get; set; } // Link back to the main search
        
        public string Title { get; set; }
        public decimal? PriceSeen { get; set; }
        public string ImageUrl { get; set; } // ADDED
    }

    [ApiController]
    [Route("api/products")]
    public class ProductTrackingController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public ProductTrackingController(IWebHostEnvironment env)
        {
            _env = env ?? throw new ArgumentNullException(nameof(env));
        }

        // NEW Endpoint for logging just the primary product
        [HttpPost("log")]
        public async Task<IActionResult> LogPrimaryProduct([FromBody] PrimaryProductLogRequest request)
        {
            if (string.IsNullOrEmpty(request?.Asin))
            {
                return BadRequest("ASIN is required.");
            }

            try
            {
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
                try
                {
                    var newRecord = new TrackedProduct
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Email = request.Email,
                        TimestampUtc = DateTime.UtcNow,
                        IpAddress = clientIp,
                        Asin = request.Asin,
                        Title = request.Title,
                        PriceSeen = request.PriceSeen,
                        ImageUrl = request.ImageUrl,
                        RecordType = request.RecordType ?? "Primary"
                    };

                    allProducts.Add(newRecord);

                    var updatedJson = JsonSerializer.Serialize(allProducts, new JsonSerializerOptions { WriteIndented = true });
                    await System.IO.File.WriteAllTextAsync(logFilePath, updatedJson);

                    return Ok(new { Message = $"Successfully logged ASIN: {request.Asin}" });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"❌ Backend Error: {ex.Message}");
                }

            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An internal server error occurred: {ex.Message}");
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
        public async Task<IActionResult> ScanAndLogProducts([FromBody] ProductScanRequest request)
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

            // Create a record for the primary ASIN
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

            // Create records for all additional ASINs
            if (request.AdditionalAsins != null)
            {
                foreach (var additionalAsin in request.AdditionalAsins)
                {
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
                }
            }

            // Write the updated list back to the file
            var updatedJson = JsonSerializer.Serialize(allProducts, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(logFilePath, updatedJson);

            return Ok(new { Message = $"Logged {1 + (request.AdditionalAsins?.Count ?? 0)} products." });
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
            public decimal? Price { get; set; }
            public string Image { get; set; }
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

        //[HttpPost("add-to-my-list")]
        //public async Task<IActionResult> AddMultipleProducts([FromBody] object raw)
        //{
        //    var logPath = Path.Combine(_env.ContentRootPath, "wwwroot", "App_Data", "RawRequest.json");

        //    try
        //    {
        //        await System.IO.File.WriteAllTextAsync(logPath, raw?.ToString());
        //        Console.WriteLine("✅ Raw request logged.");
        //        return Ok(new { message = "Request captured." });
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine("❌ ERROR logging raw request: " + ex.Message);
        //        return StatusCode(500, new { message = "Logging failed: " + ex.Message });
        //    }
        //}

        [HttpPost("add-to-my-list")]
        public async Task<IActionResult> AddMultipleProducts([FromBody] BulkProductLogRequest request)
        {
            Console.WriteLine("🚀 /api/products/add-to-my-list called");
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var logFolder = Path.Combine(_env.ContentRootPath, "wwwroot", "App_Data");

            try
            {
                // Ensure folder exists
                if (!Directory.Exists(logFolder))
                    Directory.CreateDirectory(logFolder);

                var jsonPath = Path.Combine(logFolder, "TrackedProducts.json");
                var textLogPath = Path.Combine(logFolder, "TrackedLog.txt");

                var allProducts = System.IO.File.Exists(jsonPath)
                    ? JsonSerializer.Deserialize<List<TrackedProduct>>(await System.IO.File.ReadAllTextAsync(jsonPath)) ?? new()
                    : new();

                int skipped = 0;
                var txtLines = new List<string>();

                foreach (var item in request.Items)
                {
                    if (string.IsNullOrWhiteSpace(item.Asin) || string.IsNullOrWhiteSpace(item.Title))
                    {
                        Console.WriteLine($"⚠️ Skipped: Missing title or ASIN for {item.Asin}");
                        skipped++;
                        continue;
                    }

                    var entry = new TrackedProduct
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Email = request.Email,
                        TimestampUtc = DateTime.UtcNow,
                        IpAddress = clientIp,
                        Asin = item.Asin,
                        Title = item.Title,
                        PriceSeen = item.Price,
                        ImageUrl = item.Image,
                        RecordType = request.RecordType ?? "OrderHistory"
                    };

                    allProducts.Add(entry);

                    // Optional text log line
                    txtLines.Add($"[{DateTime.UtcNow:u}] Email: {request.Email} | ASIN: {item.Asin} | Title: {item.Title} | IP: {clientIp}");
                }

                // Save updated JSON
                var updatedJson = JsonSerializer.Serialize(allProducts, new JsonSerializerOptions { WriteIndented = true });
                await System.IO.File.WriteAllTextAsync(jsonPath, updatedJson);

                // Append to .txt log
                if (txtLines.Count > 0)
                    await System.IO.File.AppendAllLinesAsync(textLogPath, txtLines);

                var message = $"Saved {request.Items.Count - skipped} items. Skipped {skipped} invalid.";
                Console.WriteLine("📦 " + message);
                return Ok(new { message });
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ ERROR during AddMultipleProducts: " + ex.Message);
                return StatusCode(500, new { message = "Server error: " + ex.Message });
            }
        }



    }
}