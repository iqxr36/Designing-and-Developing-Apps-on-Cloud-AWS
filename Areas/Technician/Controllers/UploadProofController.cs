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
    public class UploadProofController : Controller
    {
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/gif",
            "image/webp"
        };

        private readonly ApplicationDbContext _context;
        private readonly DashboardDataService _dashboardData;
        private readonly IFileStorageService _fileStorage;

        public UploadProofController(
            ApplicationDbContext context,
            DashboardDataService dashboardData,
            IFileStorageService fileStorage)
        {
            _context = context;
            _dashboardData = dashboardData;
            _fileStorage = fileStorage;
        }

        // Shows active jobs that can still receive completion proof.
        public async Task<IActionResult> Index(int? requestId)
        {
            return View(await BuildUploadModelAsync(requestId));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Saves completion proof photos to S3, marks the job completed, notifies the manager, and writes timeline history.
        public async Task<IActionResult> Upload(int requestId, string? notes, List<IFormFile>? files)
        {
            var request = await _context.MaintenanceRequests
                .Include(r => r.Assignment)
                    .ThenInclude(a => a!.Technician)
                .Include(r => r.Assignment)
                    .ThenInclude(a => a!.AssignedByManager)
                .FirstOrDefaultAsync(r =>
                    r.RequestId == requestId &&
                    r.Assignment != null &&
                    r.Assignment.Technician.UserId == CurrentUserId);

            if (request == null)
            {
                return NotFound();
            }

            var uploads = files?.Where(f => f.Length > 0).ToList() ?? new List<IFormFile>();
            if (!uploads.Any())
            {
                ModelState.AddModelError(nameof(files), "Upload at least one completion photo.");
            }

            foreach (var file in uploads)
            {
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError(nameof(files), "Only JPG, PNG, GIF, or WebP files are allowed.");
                }

                if (!string.IsNullOrWhiteSpace(file.ContentType) &&
                    !AllowedMimeTypes.Contains(file.ContentType.Trim()))
                {
                    ModelState.AddModelError(nameof(files), "Only JPG, PNG, GIF, or WebP files are allowed.");
                }

                if (file.Length > 10 * 1024 * 1024)
                {
                    ModelState.AddModelError(nameof(files), "Each proof image must be 10MB or smaller.");
                }
            }

            if (!ModelState.IsValid)
            {
                var model = await BuildUploadModelAsync(requestId);
                model.Notes = notes;
                return View("Index", model);
            }

            foreach (var file in uploads)
            {
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var fileName = $"{CurrentUserId}-{request.RequestId}-{Guid.NewGuid():N}{extension}";

                var imageUrl = await _fileStorage.UploadAsync(
                    file,
                    "proofs",
                    fileName,
                    HttpContext.RequestAborted);

                _context.RequestImages.Add(new RequestImage
                {
                    RequestId = request.RequestId,
                    UploadedByUserId = CurrentUserId,
                    ImageUrl = imageUrl,
                    ImageType = "CompletionProof",
                    UploadedAt = DateTime.UtcNow
                });
            }

            var oldStatus = request.Status;
            request.Status = "Completed";
            request.UpdatedAt = DateTime.UtcNow;

            if (request.Assignment != null)
            {
                request.Assignment.Status = "Completed";
                request.Assignment.CompletedAt ??= DateTime.UtcNow;
                request.Assignment.CompletionNotes = notes;

                if (oldStatus != "Completed")
                {
                    request.Assignment.Technician.TotalCompletedJobs += 1;
                }

                _context.Notifications.Add(new Notification
                {
                    UserId = request.Assignment.AssignedByManagerId,
                    RequestId = request.RequestId,
                    Title = "Completion proof uploaded",
                    Message = $"Proof was uploaded for REQ-{request.RequestId:0000}.",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            _context.RequestStatusHistories.Add(new RequestStatusHistory
            {
                RequestId = request.RequestId,
                ChangedByUserId = CurrentUserId,
                OldStatus = oldStatus,
                NewStatus = "Completed",
                Notes = string.IsNullOrWhiteSpace(notes) ? "Completion proof uploaded." : notes,
                ChangedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Completion proof uploaded and job marked completed.";
            return RedirectToAction("Details", "JobHistory", new { area = "Technician", id = request.RequestId });
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        // Builds the proof upload page model and chooses the selected active job.
        private async Task<TechnicianProofUploadViewModel> BuildUploadModelAsync(int? requestId)
        {
            var dashboard = await _dashboardData.GetTechnicianDashboardAsync(CurrentUserId);
            var activeJobs = dashboard.Requests
                .Where(r => r.Status != "Completed")
                .OrderByDescending(r => r.Priority == "High")
                .ThenBy(r => r.CreatedAt)
                .ToList();

            var selectedJob = requestId.HasValue
                ? activeJobs.FirstOrDefault(j => j.RequestId == requestId.Value)
                : activeJobs.FirstOrDefault();

            return new TechnicianProofUploadViewModel
            {
                ActiveJobs = activeJobs,
                SelectedJob = selectedJob,
                RequestId = selectedJob?.RequestId
            };
        }
    }
}
