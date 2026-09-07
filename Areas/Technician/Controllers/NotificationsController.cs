using CloudMVCApplication.Data;
using CloudMVCApplication.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CloudMVCApplication.Areas.Technician.Controllers
{
    [Authorize(Roles = "Technician")]
    [Area("Technician")]
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly DashboardDataService _dashboardData;

        public NotificationsController(ApplicationDbContext context, DashboardDataService dashboardData)
        {
            _context = context;
            _dashboardData = dashboardData;
        }

        public async Task<IActionResult> Index()
        {
            var model = await _dashboardData.GetTechnicianDashboardAsync(CurrentUserId);
            model.Notifications = await _context.Notifications
                .Where(n => n.UserId == CurrentUserId)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new CloudMVCApplication.ViewModels.NotificationRowViewModel(
                    n.NotificationId,
                    n.Title,
                    n.Message,
                    n.IsRead,
                    n.CreatedAt,
                    n.RequestId))
                .ToListAsync();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id, int? requestId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == CurrentUserId);

            if (notification == null)
            {
                return NotFound();
            }

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return requestId.HasValue
                ? RedirectToAction("Details", "MyJobs", new { area = "Technician", id = requestId.Value })
                : RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == CurrentUserId && !n.IsRead)
                .ToListAsync();

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Notifications marked as read.";
            return RedirectToAction(nameof(Index));
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    }
}
