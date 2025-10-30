using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace YourNamespace.Services
{
    public sealed class PaapiClient
    {
        private readonly string _accessKey;
        private readonly string _secretKey;
        private readonly string _partnerTag;
        private readonly string _region;
        private readonly string _marketplace;

        private const string Service = "ProductAdvertisingAPI";
        private const string Host = "webservices.amazon.com"; // US marketplace

        public PaapiClient(IConfiguration cfg)
        {
            var s = cfg.GetSection("AmazonPaapi");
            _accessKey = s["AccessKey"] ?? throw new InvalidOperationException("AmazonPaapi:AccessKey missing");
            _secretKey = s["SecretKey"] ?? throw new InvalidOperationException("AmazonPaapi:SecretKey missing");
            _partnerTag = s["PartnerTag"] ?? throw new InvalidOperationException("AmazonPaapi:PartnerTag missing");
            _region = s["Region"] ?? "us-east-1";
            _marketplace = s["Marketplace"] ?? "www.amazon.com";
        }

        public async Task<string> GetItemAsync(string asin)
        {
            // --- Payload (Operation is required) ---
            string payload = $@"{{
  ""ItemIds"": [""{asin}""],
  ""Resources"": [
    ""Images.Primary.Large"",
    ""ItemInfo.Title"",
    ""Offers.Listings.Price""
  ],
  ""PartnerTag"": ""{_partnerTag}"",
  ""PartnerType"": ""Associates"",
  ""Marketplace"": ""{_marketplace}"",
  ""Operation"": ""GetItems""
}}";

            string endpoint = $"https://{Host}/paapi5/getItems";
            DateTime now = DateTime.UtcNow;
            string amzDate = now.ToString("yyyyMMddTHHmmssZ");
            string dateStamp = now.ToString("yyyyMMdd");

            // --- Canonical request (headers must match what we send) ---
            string canonicalUri = "/paapi5/getItems";
            string canonicalHeaders =
                $"accept:application/json\n" +
                $"content-type:application/json; charset=utf-8\n" +
                $"host:{Host}\n" +
                $"x-amz-date:{amzDate}\n" +
                $"x-amz-target:com.amazon.paapi5.v1.ProductAdvertisingAPIv1.GetItems\n";
            string signedHeaders = "accept;content-type;host;x-amz-date;x-amz-target";
            string payloadHash = ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
            string canonicalRequest =
                $"POST\n{canonicalUri}\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";

            // --- String to sign ---
            string algorithm = "AWS4-HMAC-SHA256";
            string credentialScope = $"{dateStamp}/{_region}/{Service}/aws4_request";
            string stringToSign =
                $"{algorithm}\n{amzDate}\n{credentialScope}\n{ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))}";

            // --- Signature ---
            byte[] signingKey = GetSignatureKey(_secretKey, dateStamp, _region, Service);
            string signature = ToHex(HMACSHA256(signingKey, stringToSign));

            // --- Authorization header value ---
            string authorizationHeader =
                $"{algorithm} Credential={_accessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

            using var http = new HttpClient();

            // Clear and set only the headers we sign (plus UA)
            http.DefaultRequestHeaders.Clear();
            http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
            http.DefaultRequestHeaders.TryAddWithoutValidation("X-Amz-Target", "com.amazon.paapi5.v1.ProductAdvertisingAPIv1.GetItems");
            http.DefaultRequestHeaders.TryAddWithoutValidation("X-Amz-Date", amzDate);
            http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorizationHeader);
            // Host is sent automatically by HttpClient for HTTPS requests to this endpoint;
            // it's still part of the signature and will be present on the wire.
            http.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36"
            );

            // Content with the correct Content-Type header (this must exist since we signed it)
            var content = new StringContent(payload, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "UTF-8"
            };

            var response = await http.PostAsync(endpoint, content);

            // ---- Retry once; include diagnostics (x-amz-rid / x-amzn-RequestId) ----
            if (!response.IsSuccessStatusCode)
            {
                var body1 = await response.Content.ReadAsStringAsync();
                var rid1 = response.Headers.TryGetValues("x-amz-rid", out var ridVals1) ? string.Join(",", ridVals1) : "(none)";
                var req1 = response.Headers.TryGetValues("x-amzn-RequestId", out var reqVals1) ? string.Join(",", reqVals1) : "(none)";

                await Task.Delay(800);

                var response2 = await http.PostAsync(endpoint, content);
                var body2 = await response2.Content.ReadAsStringAsync();
                var rid2 = response2.Headers.TryGetValues("x-amz-rid", out var ridVals2) ? string.Join(",", ridVals2) : "(none)";
                var req2 = response2.Headers.TryGetValues("x-amzn-RequestId", out var reqVals2) ? string.Join(",", reqVals2) : "(none)";

                if (!response2.IsSuccessStatusCode)
                {
                    return $"Status: {(int)response2.StatusCode} {response2.ReasonPhrase}\n" +
                           $"x-amz-rid: {rid2}\n" +
                           $"x-amzn-RequestId: {req2}\n\n" +
                           $"{body2}";
                }

                return $"Status: {(int)response2.StatusCode} {response2.ReasonPhrase}\n" +
                       $"x-amz-rid: {rid2}\n" +
                       $"x-amzn-RequestId: {req2}\n\n" +
                       $"{body2}";
            }

            var okBody = await response.Content.ReadAsStringAsync();
            var rid = response.Headers.TryGetValues("x-amz-rid", out var ridVals) ? string.Join(",", ridVals) : "(none)";
            var req = response.Headers.TryGetValues("x-amzn-RequestId", out var reqVals) ? string.Join(",", reqVals) : "(none)";

            return $"Status: {(int)response.StatusCode} {response.ReasonPhrase}\n" +
                   $"x-amz-rid: {rid}\n" +
                   $"x-amzn-RequestId: {req}\n\n" +
                   $"{okBody}";
        }

        // ---------- Helpers ----------
        private static byte[] HMACSHA256(byte[] key, string data) =>
            new HMACSHA256(key).ComputeHash(Encoding.UTF8.GetBytes(data));

        private static byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
        {
            byte[] kDate = HMACSHA256(Encoding.UTF8.GetBytes("AWS4" + key), dateStamp);
            byte[] kRegion = HMACSHA256(kDate, regionName);
            byte[] kService = HMACSHA256(kRegion, serviceName);
            return HMACSHA256(kService, "aws4_request");
        }

        private static string ToHex(byte[] bytes) =>
            BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}
