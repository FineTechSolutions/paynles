namespace SmartSaverWeb.Models
{
    public class KeepaSummaryDto
    {
        public string Asin { get; set; } = "";
        public decimal Current { get; set; }
        public decimal ListPrice { get; set; }
        public decimal Average365 { get; set; }
        public decimal Highest365 { get; set; }
        public decimal Lowest365 { get; set; }
        public string Currency { get; set; } = "USD";
        public int Days { get; set; } = 365;
    }
}
