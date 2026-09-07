using Microsoft.AspNetCore.Mvc;
using CloudMVCApplication.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace CloudMVCApplication.Areas.Technician.Controllers
{
    [Authorize(Roles = "Technician")]
    [Area("Technician")]
    public class DashboardController : Controller
    {
        private readonly DashboardDataService _dashboardData;

        public DashboardController(DashboardDataService dashboardData)
        {
            _dashboardData = dashboardData;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _dashboardData.GetTechnicianDashboardAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)));
        }
    }
}
