using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartSaverWeb.DataModels;

namespace SmartSaverWeb.Controllers.Internal
{
    public class InternalController : Controller
    {
        private readonly paynles_dbContext _db;

        public InternalController(paynles_dbContext db)
        {
            _db = db;
        }
        [HttpGet]
        public IActionResult Notifications()
        {
            return View("~/Pages/Internal/Notifications.cshtml");
        }
        [HttpGet("/internal/notifications/send-queue")]
        public async Task<IActionResult> GetNotificationSendQueue()
        {
            using var conn = new SqlConnection(
                _db.Database.GetConnectionString()
            );

            var rows = await conn.QueryAsync(
                "stp_ProductNotifications_GetSendQueue",
                new { TopN = 200 },
                commandType: System.Data.CommandType.StoredProcedure
            );

            return Ok(rows);
        }



    }
}


