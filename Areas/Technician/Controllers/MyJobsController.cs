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
    public class MyJobsController : Controller
    {
        private static readonly string[] StatusOptions = { "Assigned", "In Progress", "Completed" };

        private readonly ApplicationDbContext _context;
        private readonly DashboardDataService _dashboardData;

        public MyJobsController(ApplicationDbContext context, DashboardDataService dashboardData)
        {
            _context = context;
            _dashboardData = dashboardData;
        }

        // Lists active jobs assigned to the signed-in technician.
        public async Task<IActionResult> Index()
        {
            return View(await _dashboardData.GetTechnicianDashboardAsync(CurrentUserId));
        }

        // Shows one assigned job with photos, assignment details, payment, feedback, and timeline data.
        public async Task<IActionResult> Details(int id)
        {
            var request = await LoadAssignedRequestAsync(id);
            if (request == null)
            {
                return NotFound();
            }

            return View(new TechnicianJobDetailViewModel
            {
                Request = request,
                StatusOptions = StatusOptions
            });
        }

        // Opens the status update form for a job that belongs to the signed-in technician.
        public async Task<IActionResult> UpdateStatus(int id)
        {
            var request = await LoadAssignedRequestAsync(id);
            if (request == null)
            {
                return NotFound();
            }

            return View(BuildStatusModel(request));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Updates job/request status and writes a timeline entry for managers and tenants to review.
        public async Task<IActionResult> UpdateStatus(TechnicianStatusUpdateViewModel input)
        {
            if (!StatusOptions.Contains(input.NewStatus))
            {
                ModelState.AddModelError(nameof(input.NewStatus), "Choose a valid job status.");
            }

            var request = await LoadAssignedRequestAsync(input.RequestId);
            if (request == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                var model = BuildStatusModel(request);
                model.NewStatus = input.NewStatus;
                model.Notes = input.Notes;
                return View(model);
            }

            var oldStatus = request.Status;
            request.Status = input.NewStatus;
            request.UpdatedAt = DateTime.UtcNow;

            if (request.Assignment != null)
            {
                request.Assignment.Status = input.NewStatus;
                if (input.NewStatus == "Completed")
                {
                    request.Assignment.CompletedAt ??= DateTime.UtcNow;
                    if (!string.IsNullOrWhiteSpace(input.Notes))
                    {
                        request.Assignment.CompletionNotes = input.Notes;
                    }
                }
            }

            _context.RequestStatusHistories.Add(new RequestStatusHistory
            {
                RequestId = request.RequestId,
                ChangedByUserId = CurrentUserId,
                OldStatus = oldStatus,
                NewStatus = input.NewStatus,
                Notes = input.Notes,
                ChangedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Job status updated successfully.";
            return RedirectToAction(nameof(Details), new { id = request.RequestId });
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        // Loads a request only when it is assigned to the current technician, preventing cross-technician access.
        private async Task<MaintenanceRequest?> LoadAssignedRequestAsync(int requestId)
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
                    r.Assignment != null &&
                    r.Assignment.Technician.UserId == CurrentUserId);
        }

        // Builds the form model used by both the initial status page and validation failures.
        private static TechnicianStatusUpdateViewModel BuildStatusModel(MaintenanceRequest request)
        {
            return new TechnicianStatusUpdateViewModel
            {
                RequestId = request.RequestId,
                DisplayId = $"REQ-{request.RequestId:0000}",
                Title = request.Title,
                CurrentStatus = request.Status,
                NewStatus = request.Status,
                StatusOptions = StatusOptions
            };
        }
    }
}
