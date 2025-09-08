namespace SmartSaverWeb.Models
{
    // This class is now in its own file for easy sharing.
    public class TrackedProduct
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string IpAddress { get; set; }
        public string Asin { get; set; }
        public string RecordType { get; set; }
        public string? PrimaryAsin { get; set; }
        public string Title { get; set; }
        public decimal? PriceSeen { get; set; }
        public string ImageUrl { get; set; }
    }

    // Also moving the request model here for consistency.
    public class PrimaryProductLogRequest
    {
        public string Email { get; set; }
        public string Asin { get; set; }
        public string Title { get; set; }
        public decimal? PriceSeen { get; set; }
        public string ImageUrl { get; set; }
    }
}