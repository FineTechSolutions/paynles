//using System.Text.Json.Serialization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using SmartSaverWeb.DataModels;

//namespace SmartSaverWeb.Controllers;

//[ApiController]
//[Route("api/[controller]")]
//public class ProductsController : ControllerBase
//{
//    private readonly paynles_dbContext _db;

//    public ProductsController(paynles_dbContext db)
//    {
//        _db = db;
//    }

//    // --------------------------------------------------------
//    // DTOs – these match your React types (camelCase in JSON)
//    // --------------------------------------------------------
//    public class ProductDto
//    {
//        [JsonPropertyName("asin")]
//        public string Asin { get; set; } = "";

//        [JsonPropertyName("title")]
//        public string Title { get; set; } = "";

//        [JsonPropertyName("price")]
//        public decimal Price { get; set; }

//        [JsonPropertyName("alertBelow")]
//        public decimal AlertBelow { get; set; }

//        [JsonPropertyName("imageUrl")]
//        public string ImageUrl { get; set; } = "";

//        [JsonPropertyName("tags")]
//        public List<string> Tags { get; set; } = new();

//        [JsonPropertyName("groupId")]
//        public string? GroupId { get; set; }

//        [JsonPropertyName("alertStatus")]
//        public string AlertStatus { get; set; } = "normal";

//        [JsonPropertyName("dateAdded")]
//        public DateTime? DateAdded { get; set; }
//    }

//    // --------------------------------------------------------
//    // GET /api/products?email=someone@example.com
//    // --------------------------------------------------------
//    [HttpGet]
//    public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts([FromQuery] string? email)
//    {
//        var query = _db.TrackedProducts
//            .Include(p => p.User)
//            .Include(p => p.Marketplace)
//            .Where(p => p.IsActive && !p.ToDelete);

//        if (!string.IsNullOrWhiteSpace(email))
//        {
//            query = query.Where(p => p.User.Email == email && p.User.IsActive);
//        }

//        var list = await query
//            .OrderByDescending(p => p.TimestampUtc)
//            .Select(p => new ProductDto
//            {
//                Asin = p.MarketplaceProductCode,
//                Title = p.Title ?? p.MarketplaceProductCode,
//                Price = p.PriceSeen ?? 0m,
//                AlertBelow = p.PriceSeen ?? 0m,       // TODO: real threshold column later
//                ImageUrl = p.ImageUrl ?? "",
//                Tags = new List<string>(),            // tags unsupported for now
//                GroupId = p.GroupId,
//                AlertStatus = "normal",               // TODO: compute from price vs threshold later
//                DateAdded = p.TimestampUtc
//            })
//            .ToListAsync();

//        return Ok(list);
//    }

//    // --------------------------------------------------------
//    // GET /api/products/{asin}?email=someone@example.com
//    // --------------------------------------------------------
//    [HttpGet("{asin}")]
//    public async Task<ActionResult<ProductDto>> GetProduct(
//        string asin,
//        [FromQuery] string? email)
//    {
//        var query = _db.TrackedProducts
//            .Include(p => p.User)
//            .Include(p => p.Marketplace)
//            .Where(p => p.MarketplaceProductCode == asin &&
//                        p.IsActive &&
//                        !p.ToDelete);

//        if (!string.IsNullOrWhiteSpace(email))
//        {
//            query = query.Where(p => p.User.Email == email && p.User.IsActive);
//        }

//        var p = await query.FirstOrDefaultAsync();
//        if (p == null)
//            return NotFound();

//        var dto = new ProductDto
//        {
//            Asin = p.MarketplaceProductCode,
//            Title = p.Title ?? p.MarketplaceProductCode,
//            Price = p.PriceSeen ?? 0m,
//            AlertBelow = p.PriceSeen ?? 0m,
//            ImageUrl = p.ImageUrl ?? "",
//            Tags = new List<string>(),
//            GroupId = p.GroupId,
//            AlertStatus = "normal",
//            DateAdded = p.TimestampUtc
//        };

//        return Ok(dto);
//    }
//}
