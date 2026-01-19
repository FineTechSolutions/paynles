using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using Dapper;
using Microsoft.Data.SqlClient;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SmartSaverWeb.DataModels;
namespace Paynles.Services
{
    public class ProductFileImporter
    {
        private readonly string _connectionString;
        private readonly string _filePath;

        public ProductFileImporter(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection");

            var home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
            {
                // Running in Azure App Service
                _filePath = Path.Combine(home, "data", "FilesUploaded", "TrackedProducts.json");
            }
            else
            {
                // Running locally
                _filePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "PaynLes",
                    "FilesUploaded",
                    "TrackedProducts.json"
                );
            }

            Console.WriteLine($"[ProductFileImporter] Using JSON file: {_filePath}");
        }
        public async Task<int> ImportAndRemoveAsync(string email, paynles_dbContext ctx, string marketplaceCode = "AMZ-US")
        {
            // 1️⃣  Read all matching records
            var products = await ReadProductsByEmailAsync(email);
            if (products.Count == 0)
            {
                Console.WriteLine($"[Importer] No products found for {email}");
                return 0;
            }

            // 2️⃣  Get the UserId (ensure exists)
            var user = await ctx.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                user = new SmartSaverWeb.DataModels.User
                {
                    UserId = Guid.NewGuid(),
                    Email = email,
                    CreatedUtc = DateTime.UtcNow,
                    IsActive = true
                };
                ctx.Users.Add(user);
                await ctx.SaveChangesAsync();
            }

            // 3️⃣  Find the marketplace (default Amazon US for now)
            var marketplace = await ctx.Marketplaces.FirstOrDefaultAsync(m => m.MarketplaceCode == marketplaceCode);
            if (marketplace == null)
                throw new Exception($"Marketplace not found: {marketplaceCode}");

            // 4️⃣  Insert all products with Dapper for speed
            using var connection = new SqlConnection(ctx.Database.GetConnectionString());
            await connection.OpenAsync();

            const string sql = @"
    INSERT INTO dbo.TrackedProducts
    (ProductId, UserId, MarketplaceId, MarketplaceProductCode, MarketplaceIdType,
     Title, ImageUrl, PriceSeen, RecordType, TimestampUtc, Source, IpAddress, ParentProductId, IsActive)
    VALUES
    (@ProductId, @UserId, @MarketplaceId, @MarketplaceProductCode, @MarketplaceIdType,
     @Title, @ImageUrl, @PriceSeen, @RecordType, @TimestampUtc, @Source, @IpAddress, @ParentProductId, 1);
";

            foreach (var p in products)
            {
                await connection.ExecuteAsync(sql, new
                {
                    ProductId = Guid.NewGuid(),                     // ← replaces NEWSEQUENTIALID()
                    UserId = user.UserId,
                    MarketplaceId = marketplace.MarketplaceId,
                    MarketplaceProductCode = p.Asin ?? "",
                    MarketplaceIdType = "ASIN",
                    Title = p.Title,
                    ImageUrl = p.ImageUrl,
                    PriceSeen = p.PriceSeen,
                    RecordType = p.RecordType ?? "Primary",
                    TimestampUtc = p.TimestampUtc ?? DateTime.UtcNow,
                    Source = "JSONImporter",
                    IpAddress = p.IpAddress,
                    ParentProductId = p.PrimaryAsin,                // ← matches your SQL column name
                });
            }


            // 5️⃣  Delete imported records from the JSON file
            string path;
            var home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
                path = Path.Combine(home, "data", "FilesUploaded", "TrackedProducts.json");
            else
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                    "PaynLes", "FilesUploaded", "TrackedProducts.json");

            var json = await File.ReadAllTextAsync(path);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var all = JsonSerializer.Deserialize<List<JsonProduct>>(json, opts) ?? new();

            // remove ones we just imported
            all.RemoveAll(x => x.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
            var updatedJson = JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path, updatedJson);

            Console.WriteLine($"[Importer] Imported {products.Count} and removed from file.");
            return products.Count;
        }

        public class JsonProduct
        {
            [JsonPropertyName("Id")] public string Id { get; set; }
            [JsonPropertyName("Email")] public string Email { get; set; }
            [JsonPropertyName("TimestampUtc")] public DateTime? TimestampUtc { get; set; }
            [JsonPropertyName("IpAddress")] public string IpAddress { get; set; }
            [JsonPropertyName("Asin")] public string Asin { get; set; }
            [JsonPropertyName("RecordType")] public string RecordType { get; set; }
            [JsonPropertyName("PrimaryAsin")] public string PrimaryAsin { get; set; }
            [JsonPropertyName("Title")] public string Title { get; set; }
            [JsonPropertyName("PriceSeen")] public decimal? PriceSeen { get; set; }
            [JsonPropertyName("ImageUrl")] public string ImageUrl { get; set; }
        }


        public async Task<List<JsonProduct>> ReadProductsByEmailAsync(string email)
        {
            // Resolve path: Azure vs local
            string path;
            var home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
                path = Path.Combine(home, "data", "FilesUploaded", "TrackedProducts.json");   // Azure App Service
            else
                path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "PaynLes", "FilesUploaded", "TrackedProducts.json");                       // Local debug

            Console.WriteLine($"[Importer] Reading file: {path}");

            if (!File.Exists(path))
                return new List<JsonProduct>();

            var json = await File.ReadAllTextAsync(path);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var all = JsonSerializer.Deserialize<List<JsonProduct>>(json, opts) ?? new();

            var matches = all
                .Where(x => !string.IsNullOrWhiteSpace(x.Email) &&
                            x.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
                .ToList();

            Console.WriteLine($"[Importer] Found {matches.Count} for {email}");
            return matches;
        }

        public async Task<int> ImportByEmailAsync(string Email)
        {
            if (!File.Exists(_filePath))
                return 0;

            var json = await File.ReadAllTextAsync(_filePath);
            var items = JsonSerializer.Deserialize<List<JsonProduct>>(json) ?? new List<JsonProduct>();

            var matches = items.Where(x => string.Equals(x.Email, Email, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!matches.Any()) return 0;

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                INSERT INTO dbo.TrackedProducts (Asin, Title, Price, ImageUrl, Email, RecordType, TimestampUtc)
                VALUES (@Asin, @Title, @Price, @ImageUrl, @Email, @RecordType, @TimestampUtc);
            ";

            foreach (var item in matches)
            {
                await connection.ExecuteAsync(sql, item);
            }

            return matches.Count;
        }
        public string GetConnectionMode()
        {
            if (_connectionString.Contains("Active Directory Managed Identity", StringComparison.OrdinalIgnoreCase))
                return "Managed Identity (Azure)";
            else if (_connectionString.Contains("User ID=", StringComparison.OrdinalIgnoreCase))
                return "SQL Authentication (Local)";
            else
                return "Unknown / Custom Connection Mode";
        }
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                Console.WriteLine($"✅ SQL Connection succeeded using {GetConnectionMode()}");
                await connection.CloseAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SQL Connection failed: {ex.Message}");
                return false;
            }
        }

    }
}
