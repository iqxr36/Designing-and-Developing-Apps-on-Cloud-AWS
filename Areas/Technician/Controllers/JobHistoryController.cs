using CloudMVCApplication.Data;
using CloudMVCApplication.Models;
using Microsoft.AspNetCore.Mvc;
using CloudMVCApplication.Services;
using CloudMVCApplication.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CloudMVCApplication.Areas.Technician.Controllers
{
    [Authorize(Roles = "Technician")]
    [Area("Technician")]
    public class JobHistoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly DashboardDataService _dashboardData;

        public JobHistoryController(ApplicationDbContext context, DashboardDataService dashboardData)
        {
            _context = context;
            _dashboardData = dashboardData;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _dashboardData.GetTechnicianDashboardAsync(CurrentUserId));
        }

        public async Task<IActionResult> Details(int id)
        {
            var request = await LoadCompletedRequestAsync(id);
            if (request == null)
            {
                return NotFound();
            }

            return View(new TechnicianJobDetailViewModel { Request = request });
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        private async Task<MaintenanceRequest?> LoadCompletedRequestAsync(int requestId)
        {
            return await _context.MaintenanceRequests
                .Include(r => r.Tenant)
                    .ThenInclude(t => t.User)
                .Include(r => r.Unit)
                .Include(r => r.Property)
                .Include(r => r.RequestImages)
                    .ThenInclude(i => i.UploadedByUser)
                .Include(r => r.RequestStatusHistories)
                    .ThenInclude(h => h.ChangedByUser)
                .Include(r => r.Assignment)
                    .ThenInclude(a => a!.AssignedByManager)
                .Include(r => r.Assignment)
                    .ThenInclude(a => a!.Technician)
                        .ThenInclude(t => t.User)
                .Include(r => r.ServicePayment)
                .Include(r => r.RequestFeedback)
                .FirstOrDefaultAsync(r =>
                    r.RequestId == requestId &&
                    r.Status == "Completed" &&
                    r.Assignment != null &&
                    r.Assignment.Technician.UserId == CurrentUserId);
        }
    }
}
