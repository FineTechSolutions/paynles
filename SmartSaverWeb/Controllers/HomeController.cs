using Microsoft.AspNetCore.Mvc;
using SmartSaverWeb.Models;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmartSaverWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;

        public HomeController(IConfiguration config, ILogger<HomeController> logger)
        {
            _configuration = config;
            _logger = logger;
        }

        // Home page
        public IActionResult Index() => View();

        public IActionResult About() => View();

        public IActionResult Project() => View();

        public IActionResult Referral() => View();

        public IActionResult HowItWorks() => View();

        public IActionResult Blog() => View();

        public IActionResult Testimonials() => View();

        public IActionResult Contact() => View();

        [HttpPost]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Contact(string email, string message)
        {
            try
            {
                // 1. Load SMTP config from appsettings
                var smtpServer = _configuration["Email:SmtpServer"];
                var port = int.Parse(_configuration["Email:Port"]);
                var username = _configuration["Email:Username"];
                var password = _configuration["Email:Password"];

                // 2. Create and configure SMTP client
                using var client = new SmtpClient(smtpServer, port)
                {
                    Credentials = new NetworkCredential(username, password),
                    EnableSsl = true
                };

                // 3. Compose the email
                var mail = new MailMessage
                {
                    From = new MailAddress(username, "Paynles Contact Form"),
                    Subject = "📩 New Contact Form Submission",
                    Body = $"From: {email}\n\n{message}",
                    IsBodyHtml = false
                };
                mail.To.Add("admin@paynles.com");

                // 4. Send
                client.Send(mail);

                ViewBag.Message = "✅ Thanks! Your message has been sent.";
            }
            catch (Exception ex)
            {
                ViewBag.Message = $"❌ Could not send message: {ex.Message}";
                _logger.LogError(ex, "Contact form failed to send.");
            }

            return View();
        }


        public IActionResult Privacy() => View("~/Views/Legal/Privacy.cshtml");

        public IActionResult Disclosure() => View("~/Views/Legal/Disclosure.cshtml");

        public IActionResult Terms() => View("~/Views/Legal/Terms.cshtml");

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // =========================
        // SAMPLE DEALS FROM JSON
        // =========================
        public IActionResult SampleDeals()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "FilesUploaded", "deals.json");
            List<Deal> deals = new();

            if (System.IO.File.Exists(filePath))
            {
                var json = System.IO.File.ReadAllText(filePath);
                deals = JsonSerializer.Deserialize<List<Deal>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<Deal>();

            }
            
            // ✅ Filter: remove expired + only >50% discount
            deals = deals
                .Where(d =>
                    //!string.Equals(d.DealState, "EXPIRED", StringComparison.OrdinalIgnoreCase) &&
                    d.CurrentPrice > 0 &&
                    ((d.CurrentPrice - d.DealPrice) / d.CurrentPrice) * 100 > 1
                )
                .ToList();
            var random = new Random();
            var sampleDeals = deals.OrderBy(_ => random.Next()).Take(48).ToList(); //.Take(48).ToList();

            return View(sampleDeals);
        }

        // =========================
        // ADMIN DEAL FORM
        // =========================
        [HttpGet]
        public IActionResult AdminDeals()
        {
            return View();
        }

        [HttpPost]
        public IActionResult AdminDeals(Deal newDeal)
        {
            var jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "FilesUploaded",  "deals.json");
            List<Deal> deals = new();

            if (System.IO.File.Exists(jsonPath))
            {
                var existingJson = System.IO.File.ReadAllText(jsonPath);
                deals = JsonSerializer.Deserialize<List<Deal>>(existingJson) ?? new List<Deal>();
            }

            deals.Add(newDeal);

            var updatedJson = JsonSerializer.Serialize(deals, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(jsonPath, updatedJson);

            ViewBag.Message = "Deal added successfully!";
            return View();
        }

        // =========================
        // KEEPA IMPORT BUTTON
        // =========================
        [HttpPost]
        public async Task<IActionResult> ImportKeepaDeals()
        {
            try
            {
                string apiKey = _configuration["Keepa:ApiKey"];
                string url = $"https://api.keepa.com/lightningdeal?key={apiKey}&domain=1";
                //string url = $"https://api.keepa.com/deal?key={apiKey}&domain=1&page=0";
                //string url = $"https://api.keepa.com/product?key={apiKey}&domain=1&asin=B0FC5TJSXN&buybox=1&history=1";
                using var client = new HttpClient();
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    ViewBag.Message = $"Keepa fetch failed: {response.StatusCode} — {err}";
                    return View("AdminDeals");
                }

               // var json = await response.Content.ReadAsStringAsync();
                using var stream = await response.Content.ReadAsStreamAsync();
                using var gzip = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress);
                using var reader = new StreamReader(gzip);
                var json = await reader.ReadToEndAsync();

                string logPath = @"C:\fts\keepa_response_debug.json";
                System.IO.File.WriteAllText(logPath, json);
                var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("products", out var products))
                {
                    ViewBag.Message = "No 'products' array found in response.";
                    return View("AdminDeals");
                }

                var deals = new List<Deal>();

                foreach (var product in products.EnumerateArray().Take(1)) // Only take 1 deal
                {
                    if (!product.TryGetProperty("asin", out var asinProp)) continue;
                    if (!product.TryGetProperty("title", out var titleProp)) continue;

                    string asin = asinProp.GetString() ?? "";
                    string title = titleProp.GetString() ?? "Unknown";
                    decimal? currentPrice = null;
                    decimal? originalPrice = null;

                    if (product.TryGetProperty("price", out var priceProp) &&
                        priceProp.ValueKind == JsonValueKind.Number && priceProp.GetDecimal() > 0)
                    {
                        currentPrice = priceProp.GetDecimal() / 100;
                    }

                    if (product.TryGetProperty("listPrice", out var listProp) &&
                        listProp.ValueKind == JsonValueKind.Number && listProp.GetDecimal() > 0)
                    {
                        originalPrice = listProp.GetDecimal() / 100;
                    }

 string brand = product.TryGetProperty("brand", out var brandProp) ? brandProp.GetString() ?? "" : "";
                    // Fallback to some average or known current
                                        decimal discountedPrice = 0;

                    if (product.TryGetProperty("listPrice", out var listPriceProp))
                        originalPrice = listPriceProp.GetDecimal() / 100M;

                    if (product.TryGetProperty("buyBoxPrice", out var buyboxProp))
                        discountedPrice = buyboxProp.GetDecimal() / 100M;
                    if (currentPrice == null || originalPrice == null || currentPrice >= originalPrice) continue;


                    deals.Add(new Deal
                    {
                        Asin = asin,
                        Title = title.Length > 50 ? title.Substring(0, 50) : title,
                        CurrentPrice = originalPrice ?? 0,
                        DealPrice = currentPrice ?? 0,
                        DomainId = 1, // Amazon US
                        Image = $"https://images-na.ssl-images-amazon.com/images/P/{asin}.jpg",
                        IsPrimeEligible = product.TryGetProperty("isPrime", out var primeProp) && primeProp.GetBoolean(),
                        Rating = product.TryGetProperty("rating", out var ratingProp) ? ratingProp.GetDouble() : 0,
                        TotalReviews = product.TryGetProperty("reviewCount", out var reviewsProp) ? reviewsProp.GetInt32() : 0
                    });

                }

                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "FilesUploaded", "deals.json");
                System.IO.File.WriteAllText(filePath, JsonSerializer.Serialize(deals, new JsonSerializerOptions { WriteIndented = true }));

                ViewBag.Message = $"✅ {deals.Count} lightning deal saved to deals.json.";
                return View("AdminDeals");
            }
            catch (Exception ex)
            {
                ViewBag.Message = $"Error importing Keepa deals: {ex.Message}";
                return View("AdminDeals");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CheckKeepaTokens()
        {
            string apiKey = _configuration["Keepa:ApiKey"];
            string url = $"https://api.keepa.com/token?key={apiKey}";

            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            using var client = new HttpClient(handler);
            var response = await client.GetAsync(url);

            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Message = $"Keepa token check failed: {response.StatusCode} — {json}";
                return View("AdminDeals");
            }

            var doc = JsonDocument.Parse(json);
            int tokensLeft = doc.RootElement.GetProperty("tokensLeft").GetInt32();
            ViewBag.Message = $"Keepa tokens available: {tokensLeft}";

            return View("AdminDeals");
        }

    }
}
