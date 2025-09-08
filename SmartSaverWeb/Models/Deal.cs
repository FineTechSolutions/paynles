namespace SmartSaverWeb.Models
{
    public class Deal
    {
        public string Asin { get; set; }
        public string Title { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal DealPrice { get; set; }
        public int DomainId { get; set; }
        public string Image { get; set; }
        public bool IsPrimeEligible { get; set; }
        public double Rating { get; set; }
        public int TotalReviews { get; set; }
        public bool isPrimeEarlyAccess { get; set; }
        public string DealState { get; set; }
    }
}
