using Microsoft.AspNetCore.Mvc;
using SmartSaverWeb.Models;
using SmartSaverWeb.Services;

namespace SmartSaverWeb.Controllers
{
    [ApiController]
    [Route("api/pricing")]
    public class KeepaController : ControllerBase
    {
        private readonly KeepaClient _client;

        public KeepaController(KeepaClient client)
        {
            _client = client;
        }

        [HttpGet("keepa-summary")]
        public async Task<ActionResult<KeepaSummaryDto>> GetKeepaSummary([FromQuery] string asin)
        {
            if (string.IsNullOrWhiteSpace(asin))
                return BadRequest("Missing ASIN");

            var product = await _client.GetProductSummaryAsync(asin); // <-- next step will define this

            if (product == null)
                return NotFound();

            return Ok(product);
        }
    }
}
