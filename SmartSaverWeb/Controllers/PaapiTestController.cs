using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using YourNamespace.Services;

namespace YourNamespace.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaapiTestController : ControllerBase
    {
        private readonly PaapiClient _client;
        public PaapiTestController(PaapiClient client) => _client = client;

        // GET /api/paapitest/B07FZ8S74R
        [HttpGet("{asin}")]
        public async Task<IActionResult> Get(string asin)
        {
            if (string.IsNullOrWhiteSpace(asin))
                return BadRequest("Provide an ASIN.");

            var json = await _client.GetItemAsync(asin);
            // If the service returned a "Status: ..." string, just forward it.
            if (json.StartsWith("Status: "))
                return Content(json, "text/plain");

            // Otherwise it's JSON from PA-API
            return Content(json, "application/json");
        }
    }
}
