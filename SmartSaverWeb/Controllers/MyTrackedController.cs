//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using SmartSaverWeb.DataModels;

//namespace SmartSaverWeb.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public class MyTrackedController : ControllerBase
//    {
//        private readonly paynles_dbContext _ctx;

//        public MyTrackedController(paynles_dbContext ctx)
//        {
//            _ctx = ctx;
//        }
//        // 🔹 Keep selected items (set Keep = 1)
//        [HttpPost("keep")]
//        public async Task<IActionResult> KeepSelected([FromBody] List<Guid> productIds)
//        {
//            if (productIds == null || productIds.Count == 0)
//                return BadRequest("No ProductIds received.");

//            var products = await _ctx.TrackedProducts
//                .Where(p => productIds.Contains(p.ProductId))
//                .ToListAsync();

//            if (!products.Any())
//                return NotFound("No matching products found.");

//            foreach (var p in products)
//                p.Keep = true;

//            await _ctx.SaveChangesAsync();

//            return Ok(new { success = true, updated = products.Count });
//        }

//        // 🔹 Dispose selected items (set ToDelete = 1 and DateMarkedToDelete)
//        [HttpPost("dispose")]
//        public async Task<IActionResult> DisposeSelected([FromBody] List<Guid> productIds)
//        {
//            if (productIds == null || productIds.Count == 0)
//                return BadRequest("No ProductIds received.");

//            var products = await _ctx.TrackedProducts
//                .Where(p => productIds.Contains(p.ProductId))
//                .ToListAsync();

//            if (!products.Any())
//                return NotFound("No matching products found.");

//            foreach (var p in products)
//            {
//                p.ToDelete = true;
//                p.DateMarkedToDelete = DateTime.UtcNow;
//            }

//            await _ctx.SaveChangesAsync();

//            return Ok(new { success = true, updated = products.Count });
//        }

//    }
//}
