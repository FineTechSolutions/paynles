public class PriceUpdateRunOptions
{
    // Only check products not updated within this window
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromHours(24);

    // Max number of products to process in one run
    public int MaxProducts { get; set; } = 200;

    // Optional override (manual run, debug, etc.)
    public IReadOnlyCollection<Guid>? MarketplaceIds { get; set; }

    // If true, ignore staleness and force refresh
    public bool ForceRefresh { get; set; } = false;
}
public class PriceUpdateRunResult
{
    public DateTime StartedUtc { get; set; }
    public DateTime FinishedUtc { get; set; }

    public int ProductsConsidered { get; set; }
    public int ProductsChecked { get; set; }

    public int PricesChanged { get; set; }
    public int PricesUnchanged { get; set; }

    public int Failures { get; set; }
    public List<string> ProductsProcessed { get; set; } = new();

    public List<PriceUpdateFailure> FailureDetails { get; set; } = new();
}
public class PriceUpdateFailure
{
    public Guid ProductId { get; set; }
    public string MarketplaceProductCode { get; set; } = "";
    public string Reason { get; set; } = "";
}
