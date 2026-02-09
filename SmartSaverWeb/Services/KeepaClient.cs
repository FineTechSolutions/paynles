using System.Text.Json;
using System.Text.Json.Serialization;
using SmartSaverWeb.Models;

namespace SmartSaverWeb.Services
{
    public class KeepaClient
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        public KeepaClient(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;
            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            _http.DefaultRequestHeaders.AcceptEncoding.Clear();
        }

        public async Task<KeepaSummaryDto?> GetProductSummaryAsync(string asin)
        {
            string apiKey = _config["Keepa:ApiKey"] ?? throw new Exception("Keepa key missing");
            int domain = int.TryParse(_config["Keepa:DomainId"], out var d) ? d : 1;

            var url = $"https://api.keepa.com/product?key={apiKey}&domain={domain}&asin={asin}&buybox=1&stats=365&csv=4";

            using var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();

            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };

            var root = JsonSerializer.Deserialize<KeepaRoot>(json, opts);
            var product = root?.Products?.FirstOrDefault();
            var s = product?.Stats;

#if DEBUG
            Console.WriteLine("--- Raw Keepa stats ---");
            Console.WriteLine("ASIN: " + asin);
            Console.WriteLine("stats.listPrice: " + s?.ListPrice);

            string Join(int[]? arr) => arr == null ? "null" : string.Join(", ", arr);
            string Join2D(int[][]? arr) =>
                arr == null ? "null" : string.Join(" | ", arr.Select(a => a == null ? "null" : string.Join(",", a)));

            Console.WriteLine("stats.current: " + Join(s?.Current));
            Console.WriteLine("stats.avg365: " + Join(s?.Avg365));
            Console.WriteLine("stats.minInInterval: " + Join2D(s?.MinInInterval));
            Console.WriteLine("stats.maxInInterval: " + Join2D(s?.MaxInInterval));
#endif

         
            if (s == null) return null;

            var buyBoxCurrent = ToMoney(At1D(s.Current, 1));
            var buyBoxAvg365 = ToMoney(At1D(s.Avg365, 1));
            var buyBoxLowest365 = ToMoney(At2D(s.MinInInterval, 1, 1));
            var buyBoxHighest365 = ToMoney(At2D(s.MaxInInterval, 1, 1));
            var listPriceCurrent = ToMoney(At1D(s.Current, 4));

            return new KeepaSummaryDto
            {
                Asin = asin,
                ListPrice = Math.Round(listPriceCurrent, 2),
                Current = Math.Round(buyBoxCurrent, 2),
                Average365 = Math.Round(buyBoxAvg365, 2),
                Lowest365 = Math.Round(buyBoxLowest365, 2),
                Highest365 = Math.Round(buyBoxHighest365, 2),
                Currency = "USD",
                Days = 365
            };
        }

        // ---------- Helpers ----------
        public async Task<int> GetTokenBalanceForInternalUseAsync()
        {
            string apiKey = _config["Keepa:ApiKey"]
                ?? throw new Exception("Keepa key missing");

            string url = $"https://api.keepa.com/token?key={apiKey}";

            using var response = await _http.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Keepa token check failed: {response.StatusCode} — {json}");
            }

            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("tokensLeft", out var tokens))
            {
                throw new Exception("tokensLeft not found in Keepa response");
            }

            return tokens.GetInt32();
        }

        private static decimal ToMoney(int? cents) =>
            (cents.HasValue && cents.Value > 0) ? cents.Value / 100m : 0m;

        private static int? At1D(int[]? arr, int i) =>
            (arr != null && i >= 0 && i < arr.Length) ? arr[i] : null;

        private static int? At2D(int[][]? arr, int i, int j) =>
            (arr != null && i >= 0 && i < arr.Length &&
             arr[i] != null && j >= 0 && j < arr[i].Length)
                ? arr[i][j]
                : null;

        // ---------- JSON POCOs ----------

        private sealed class KeepaRoot
        {
            [JsonPropertyName("products")]
            public List<Product> Products { get; set; } = new();
        }

        private sealed class Product
        {
            [JsonPropertyName("stats")]
            public Stats Stats { get; set; } = default!;
        }

        private sealed class Stats
        {
            [JsonPropertyName("listPrice")]
            public int? ListPrice { get; set; }

            [JsonPropertyName("current")]
            public int[]? Current { get; set; }

            [JsonPropertyName("avg365")]
            public int[]? Avg365 { get; set; }

            [JsonPropertyName("minInInterval")]
            public int[][]? MinInInterval { get; set; }

            [JsonPropertyName("maxInInterval")]
            public int[][]? MaxInInterval { get; set; }
        }
    }
}
