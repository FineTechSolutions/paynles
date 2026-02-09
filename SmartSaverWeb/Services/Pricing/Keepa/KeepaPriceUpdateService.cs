using Microsoft.EntityFrameworkCore;
using SmartSaverWeb.DataModels;
using System.Linq;
using SmartSaverWeb.Services;
namespace SmartSaverWeb.Services.Pricing.Keepa
{
    public class KeepaPriceUpdateService
    {
        private readonly paynles_dbContext _db;
        private readonly KeepaClient _keepaClient;

        public KeepaPriceUpdateService(
     paynles_dbContext db,
     KeepaClient keepaClient)
        {
            _db = db;
            _keepaClient = keepaClient;
        }
        public async Task<PriceUpdateRunResult> RunAsync(
       PriceUpdateRunOptions options,
       CancellationToken cancellationToken = default)
        {
            var result = new PriceUpdateRunResult
            {
                StartedUtc = DateTime.UtcNow
            };
            // ---- Keepa token safety check (internal use only)
            int tokensLeft = await _keepaClient.GetTokenBalanceForInternalUseAsync();

            if (tokensLeft < 10)
            {
                result.FinishedUtc = DateTime.UtcNow;
                result.Failures = 1;
                result.FailureDetails.Add(new PriceUpdateFailure
                {
                    Reason = $"Aborted: Keepa tokens too low ({tokensLeft})"
                });

                return result;
            }

            var staleBefore = DateTime.UtcNow.Subtract(options.StaleAfter);

            var query =
                from tp in _db.TrackedProducts
                join noti in _db.ProductNotifications
                    on tp.ProductId equals noti.ProductId
                where tp.ToDelete == false
                      && noti.IsActive
                      && tp.MarketplaceProductCode != null
                      && (
                            options.ForceRefresh
                            || tp.DatePriceChecked == null
                            || tp.DatePriceChecked <= staleBefore
                         )
                orderby tp.DatePriceChecked
                select tp;

            if (options.MarketplaceIds?.Any() == true)
            {
                query = query.Where(tp => options.MarketplaceIds.Contains(tp.MarketplaceId));
            }

            var products = await query
                .Take(options.MaxProducts)
                .ToListAsync(cancellationToken);
            foreach (var product in products)
            {
                // Stop immediately if tokens are low
                int tokensLeftNow = await _keepaClient.GetTokenBalanceForInternalUseAsync();
                if (tokensLeftNow < 10)
                {
                    result.FailureDetails.Add(new PriceUpdateFailure
                    {
                        ProductId = product.ProductId,
                        MarketplaceProductCode = product.MarketplaceProductCode,
                        Reason = $"Aborted run: Keepa tokens too low ({tokensLeftNow})"
                    });
                    break;
                }

                try
                {
                    result.ProductsProcessed.Add(product.MarketplaceProductCode);

                    var summary = await _keepaClient
                        .GetProductSummaryAsync(product.MarketplaceProductCode);

                    if (summary == null)
                    {
                        result.Failures++;
                        result.FailureDetails.Add(new PriceUpdateFailure
                        {
                            ProductId = product.ProductId,
                            MarketplaceProductCode = product.MarketplaceProductCode,
                            Reason = "Keepa returned null summary"
                        });
                        continue;
                    }

                    product.ListPrice = summary.ListPrice;
                    product.PriceAverage = summary.Average365;
                    product.PriceHighest = summary.Highest365;
                    product.PriceLowest = summary.Lowest365;
                    product.PriceDays = summary.Days;
                    product.PriceSeen = summary.Current;

                    product.DatePriceChecked = DateTime.UtcNow;

                    await _db.SaveChangesAsync(cancellationToken);

                    result.PricesUnchanged++;
                }
                catch (Exception ex)
                {
                    result.Failures++;
                    result.FailureDetails.Add(new PriceUpdateFailure
                    {
                        ProductId = product.ProductId,
                        MarketplaceProductCode = product.MarketplaceProductCode,
                        Reason = ex.Message
                    });
                }
            }



            result.ProductsConsidered = products.Count;
            result.ProductsChecked = products.Count;

            result.FinishedUtc = DateTime.UtcNow;

            return result;
        }


    }

}
