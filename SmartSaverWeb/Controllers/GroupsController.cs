//// --- DEPRECATED: merged into TrackedGridController.cs ---
//// This controller is temporarily disabled to avoid duplicate routes.
//using System.Text.Json.Serialization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using SmartSaverWeb.DataModels;

//namespace SmartSaverWeb.Controllers;

//[ApiController]
//[Route("api/[controller]")]
//public class GroupsController : ControllerBase
//{
//    private readonly paynles_dbContext _db;

//    public GroupsController(paynles_dbContext db)
//    {
//        _db = db;
//    }

//    public class GroupDto
//    {
//        [JsonPropertyName("id")]
//        public string Id { get; set; } = "";

//        [JsonPropertyName("name")]
//        public string Name { get; set; } = "";

//        [JsonPropertyName("parentId")]
//        public string? ParentId { get; set; }

//        [JsonPropertyName("icon")]
//        public string? Icon { get; set; }

//        [JsonPropertyName("collectionType")]
//        public string? CollectionType { get; set; } = "manual";
//    }

//    // GET /api/groups?email=someone@example.com
//    [HttpGet]
//    public async Task<ActionResult<IEnumerable<GroupDto>>> GetGroups([FromQuery] string? email)
//    {
//        var query = _db.TrackedProducts
//            .Include(p => p.User)
//            .Where(p => p.IsActive && !p.ToDelete && p.GroupId != null && p.GroupId != "");

//        if (!string.IsNullOrWhiteSpace(email))
//        {
//            query = query.Where(p => p.User.Email == email && p.User.IsActive);
//        }

//        var groupIds = await query
//            .Select(p => p.GroupId)
//            .Distinct()
//            .OrderBy(g => g)
//            .ToListAsync();

//        var groups = groupIds
//            .Select(id => new GroupDto
//            {
//                Id = id!,
//                Name = id!,
//                ParentId = null,
//                Icon = null,
//                CollectionType = "manual"
//            })
//            .ToList();

//        return Ok(groups);
//    }
//}
