using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using SmartSaverWeb.Models;
namespace YourNamespace.Controllers
{
    [ApiController]
    [Route("api/add-deal")]
    public class AddDealController : ControllerBase
    {
//        public class Deal
//{
//    public string Asin { get; set; }
//    public string Title { get; set; }
//    public decimal CurrentPrice { get; set; }
//    public decimal DealPrice { get; set; }
//    public int DomainId { get; set; }
//    public string Image { get; set; }
//    public bool IsPrimeEligible { get; set; }
//    public double Rating { get; set; }
//    public int TotalReviews { get; set; }
//            public string Badge { get; set; }
//        }


        [HttpPost]
        public async Task<IActionResult> Add([FromBody] SmartSaverWeb.Models.Deal newDeal)

        {
            if (newDeal == null)
            {
                return BadRequest("Missing or invalid deal data.");
            }

            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deals.json");
            List<Deal> deals;

            if (System.IO.File.Exists(filePath))
            {
                var content = await System.IO.File.ReadAllTextAsync(filePath);
                deals = JsonSerializer.Deserialize<List<Deal>>(content) ?? new List<Deal>();
            }
            else
            {
                deals = new List<Deal>();
            }

            deals.Add(newDeal);

            var updated = JsonSerializer.Serialize(deals, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await System.IO.File.WriteAllTextAsync(filePath, updated);
            return Ok();
        }
    }
}
