using Microsoft.AspNetCore.Mvc;
using SmartSaverWeb.Services.Pricing.Keepa;

namespace SmartSaverWeb.Controllers.Internal
{
    [ApiController]
    [Route("internal/price-update")]
    public class PriceUpdateController : ControllerBase
    {
        private readonly KeepaPriceUpdateService _service;

        public PriceUpdateController(KeepaPriceUpdateService service)
        {
            _service = service;
        }

        [HttpGet("dry-run")]
        public async Task<IActionResult> DryRun(CancellationToken cancellationToken)
        {
            var options = new PriceUpdateRunOptions
            {
                StaleAfter = TimeSpan.FromHours(24),
                MaxProducts = 25
            };

            var result = await _service.RunAsync(options, cancellationToken);

            return Ok(result);
        }
    }
}
