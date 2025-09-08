using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using SmartSaverWeb.Models;
using System.Text.Json;

namespace SmartSaverWeb.Pages
{
    public class MyProductsModel : PageModel
    {
        private static readonly object _fileLock = new();
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<MyProductsModel> _logger;
        private readonly string _logFilePath;

        public List<Product> Products { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        [FromQuery(Name = "email")]
        public string Email { get; set; }

        [BindProperty(SupportsGet = true)]
        [FromQuery(Name = "debug")]
        public int Debug { get; set; }

        [BindProperty] // ✅ Bind POST field "asin" to this
        public string Asin { get; set; }

        public string DataPath { get; private set; } = "";
        public int TotalRead { get; private set; } = 0;
        public string LastError { get; private set; } = "";

        public MyProductsModel(IWebHostEnvironment env, ILogger<MyProductsModel> logger)
        {
            _env = env;
            _logger = logger;
            _logFilePath = Path.Combine(_env.ContentRootPath, "wwwroot", "App_Data", "TrackedProducts.json");
        }

        public async Task OnGetAsync()
        {
            Email ??= RouteData.Values.TryGetValue("email", out var routeEmail) ? routeEmail?.ToString() : null;
            Email = Email?.Trim();

            DataPath = _logFilePath;

            if (string.IsNullOrWhiteSpace(Email))
            {
                Products = new();
                return;
            }

            Products = await LoadProductsForEmailAsync(Email);
        }


        public async Task<IActionResult> OnPostDeleteAsync()
        {
            System.Diagnostics.Debug.WriteLine("DELETE HANDLER CALLED"); // ✅ Step 1: prove handler run
            if (!ModelState.IsValid)
            {
                System.Diagnostics.Debug.WriteLine("❌ Model state is invalid");
                return BadRequest("Invalid form data");
            }

            // Optional: log the values
            System.Diagnostics.Debug.WriteLine($"Email = {Email}, Asin = {Asin}");
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Asin))
            {
                return BadRequest("Missing email or asin");
            }

            var path = Path.Combine(_env.WebRootPath, "App_Data", "TrackedProducts.json");

            if (!System.IO.File.Exists(path))
            {
                return NotFound("Data file missing");
            }

            var json = await System.IO.File.ReadAllTextAsync(path);
            var list = JsonSerializer.Deserialize<List<Product>>(json) ?? new();

            // ✅ Remove only the first matching entry for this email + asin
            var target = list.FirstOrDefault(p =>
                p.Email.Equals(Email, StringComparison.OrdinalIgnoreCase) &&
                p.Asin.Equals(Asin, StringComparison.OrdinalIgnoreCase));

            if (target != null)
            {
                list.Remove(target);

                await System.IO.File.WriteAllTextAsync(path,
                    JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
            }

            return RedirectToPage(new { email = Email });
        }



        private async Task<List<Product>> LoadProductsForEmailAsync(string email)
        {
            var list = new List<Product>();

            try
            {
                if (!System.IO.File.Exists(_logFilePath))
                {
                    LastError = "File not found.";
                    return list;
                }

                var json = await System.IO.File.ReadAllTextAsync(_logFilePath);
                var all = JsonSerializer.Deserialize<List<Product>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                          ?? new List<Product>();

                TotalRead = all.Count;

                list = all
                    .Where(p => p != null
                                && string.Equals(p.Email, email, StringComparison.OrdinalIgnoreCase)
                                && !string.IsNullOrWhiteSpace(p.Asin))
                    .OrderByDescending(p => p.TimestampUtc)
                    .GroupBy(p => p.Asin)
                    .Select(g => g.First())
                    .OrderByDescending(p => p.TimestampUtc)
                    .ToList();
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                _logger.LogError(ex, "Failed to load or parse products.");
            }

            return list;
        }
    }
}
