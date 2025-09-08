namespace SmartSaverWeb.Models
{
    public class Product
    {
        public string Id { get; set; }
        public string Asin { get; set; }
        public string Title { get; set; }
        public string ImageUrl { get; set; }
        public decimal? PriceSeen { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string Email { get; set; }
    }
}