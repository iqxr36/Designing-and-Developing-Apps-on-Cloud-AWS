using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using CloudMVCApplication.Data;
using CloudMVCApplication.Models;
using CloudMVCApplication.Services;
using CloudMVCApplication.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CloudMVCApplication.Areas.Tenant.Controllers
{
    [Authorize(Roles = "Tenant")]
    [Area("Tenant")]
    [Route("[area]/[action]")]
    public class TenantController : Controller
    {
        private static readonly string[] RequestCategories = { "Plumbing", "Electrical", "HVAC", "Appliance", "General", "Other" };
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private static readonly HashSet<string> AllowedImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/gif",
            "image/webp"
        };

        private readonly ApplicationDbContext _context;
        private readonly DashboardDataService _dashboardData;
        private readonly MessagingService _messagingService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IFileStorageService _fileStorage;
        private readonly IAvatarService _avatarService;

        public TenantController(
            ApplicationDbContext context,
            DashboardDataService dashboardData,
            MessagingService messagingService,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IFileStorageService fileStorage,
            IAvatarService avatarService)
        {
            _context = context;
            _dashboardData = dashboardData;
            _messagingService = messagingService;
            _userManager = userManager;
            _signInManager = signInManager;
            _fileStorage = fileStorage;
            _avatarService = avatarService;
        }

        public Task<IActionResult> Dashboard() => TenantViewAsync();

        public Task<IActionResult> SubmitMaintenanceRequest() => TenantViewAsync();

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Creates a new tenant maintenance request, saves optional issue photos, opens support chat, and notifies managers.
        public async Task<IActionResult> SubmitMaintenanceRequest(TenantMaintenanceRequestViewModel input)
        {
            ValidateCategory(input.Category);
            ValidateIssuePhotoFiles(input.Files, nameof(input.Files), required: false);

            var tenant = await GetCurrentTenantProfileAsync();
            if (tenant == null)
            {
                return Challenge();
            }

            if (!ModelState.IsValid)
            {
                return View("~/Areas/Tenant/Views/SubmitMaintenanceRequest.cshtml", await BuildTenantModelAsync());
            }

            var request = new MaintenanceRequest
            {
                TenantId = tenant.TenantId,
                UnitId = tenant.UnitId,
                PropertyId = tenant.Unit.PropertyId,
                Title = input.Title.Trim(),
                Description = input.Description.Trim(),
                Category = input.Category,
                Priority = "Medium",
                Status = "Pending"
            };

            _context.MaintenanceRequests.Add(request);
            await _context.SaveChangesAsync();

            _context.RequestStatusHistories.Add(new RequestStatusHistory
            {
                RequestId = request.RequestId,
                ChangedByUserId = CurrentUserId,
                OldStatus = null,
                NewStatus = request.Status,
                Notes = "Request submitted by tenant."
            });

            _context.Notifications.Add(new Notification
            {
                UserId = tenant.UserId,
                RequestId = request.RequestId,
                Title = "Maintenance request submitted",
                Message = $"Your request {FormatRequestId(request.RequestId)} was submitted successfully."
            });

            await NotifyManagersAsync(
                tenant.Unit.PropertyId,
                tenant.User.CompanyId ?? 0,
                request.RequestId,
                "New tenant maintenance request",
                $"{tenant.User.FullName} submitted {FormatRequestId(request.RequestId)}: {request.Title}.");

            var uploads = input.Files?.Where(f => f.Length > 0).ToList() ?? new List<IFormFile>();
            if (uploads.Any())
            {
                await SaveIssuePhotosAsync(request, tenant, uploads);
            }

            await _context.SaveChangesAsync();
            await _messagingService.CreateTenantSupportConversationAsync(request.RequestId);

            TempData["SuccessMessage"] = uploads.Any()
                ? $"Request {FormatRequestId(request.RequestId)} submitted with {uploads.Count} photo(s)."
                : $"Request {FormatRequestId(request.RequestId)} submitted successfully.";
            return RedirectToAction(nameof(TrackRequestStatus));
        }

        public IActionResult UploadIssuePhotos(int? requestId)
        {
            if (requestId.HasValue)
            {
                return RedirectToAction(nameof(RequestDetail), new { id = requestId.Value });
            }

            return RedirectToAction(nameof(SubmitMaintenanceRequest));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Adds more issue photos to an open request and records that upload in the request timeline.
        public async Task<IActionResult> UploadIssuePhotos(TenantIssuePhotoUploadViewModel input)
        {
            var tenant = await GetCurrentTenantProfileAsync();
            if (tenant == null)
            {
                return Challenge();
            }

            var request = await GetTenantRequestEntityAsync(input.RequestId, tenant.TenantId);
            if (request == null)
            {
                return NotFound();
            }

            if (request.Status is "Completed" or "Cancelled")
            {
                ModelState.AddModelError(string.Empty, "You can only upload photos for open requests.");
            }

            ValidateIssuePhotoFiles(input.Files, nameof(input.Files), required: true);

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(RequestDetail), new { id = input.RequestId });
            }

            var uploads = input.Files.Where(f => f.Length > 0).ToList();
            await SaveIssuePhotosAsync(request, tenant, uploads);
            _context.RequestStatusHistories.Add(new RequestStatusHistory
            {
                RequestId = request.RequestId,
                ChangedByUserId = CurrentUserId,
                OldStatus = request.Status,
                NewStatus = request.Status,
                Notes = $"{uploads.Count} issue photo(s) uploaded by tenant.",
                ChangedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Issue photos uploaded successfully.";
            return RedirectToAction(nameof(RequestDetail), new { id = input.RequestId });
        }

        public Task<IActionResult> TrackRequestStatus() => TenantViewAsync();

        [HttpGet("{id:int}")]
        // Shows one tenant-owned request with timeline and uploaded evidence.
        public async Task<IActionResult> RequestDetail(int id)
        {
            var model = await BuildTenantModelAsync(id);
            return model.SelectedRequest == null
                ? NotFound()
                : View("~/Areas/Tenant/Views/RequestDetail.cshtml", model);
        }

        public Task<IActionResult> ViewUnitDetails() => TenantViewAsync();

        // Shows completed requests that are still eligible for tenant feedback.
        public async Task<IActionResult> SubmitFeedback()
        {
            var model = await BuildTenantModelAsync();
            ViewBag.FeedbackRequestIds = await GetFeedbackSubmittedRequestIdsAsync();
            return View("~/Areas/Tenant/Views/SubmitFeedback.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Stores tenant feedback for a completed request and recalculates the technician's average rating.
        public async Task<IActionResult> SubmitFeedback(TenantFeedbackViewModel input)
        {
            var tenant = await GetCurrentTenantProfileAsync();
            if (tenant == null)
            {
                return Challenge();
            }

            var request = await _context.MaintenanceRequests
                .Include(r => r.Assignment)
                .Include(r => r.RequestFeedback)
                .FirstOrDefaultAsync(r => r.RequestId == input.RequestId && r.TenantId == tenant.TenantId);

            if (request == null)
            {
                return NotFound();
            }

            if (request.Status != "Completed")
            {
                ModelState.AddModelError(string.Empty, "You can only submit feedback for completed requests.");
            }

            if (request.Assignment == null)
            {
                ModelState.AddModelError(string.Empty, "This request has no assigned technician to review.");
            }

            if (request.RequestFeedback != null)
            {
                ModelState.AddModelError(string.Empty, "Feedback has already been submitted for this request.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.FeedbackRequestIds = await GetFeedbackSubmittedRequestIdsAsync();
                return View("~/Areas/Tenant/Views/SubmitFeedback.cshtml", await BuildTenantModelAsync());
            }

            _context.RequestFeedbacks.Add(new RequestFeedback
            {
                RequestId = request.RequestId,
                TenantId = tenant.TenantId,
                TechnicianId = request.Assignment!.TechnicianId,
                Rating = input.Rating,
                Comment = string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim()
            });

            await _context.SaveChangesAsync();
            await RecalculateTechnicianRatingAsync(request.Assignment.TechnicianId);

            var technician = await _context.TechnicianProfiles
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TechnicianId == request.Assignment.TechnicianId);

            if (technician != null)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = technician.UserId,
                    RequestId = request.RequestId,
                    Title = "New tenant feedback",
                    Message = $"{tenant.User.FullName} rated {FormatRequestId(request.RequestId)} {input.Rating}/5."
                });
            }

            var managerIds = await GetManagerUserIdsForPropertyAsync(request.PropertyId, tenant.User.CompanyId);
            foreach (var managerId in managerIds)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = managerId,
                    RequestId = request.RequestId,
                    Title = "Tenant feedback received",
                    Message = $"{tenant.User.FullName} submitted feedback for {FormatRequestId(request.RequestId)}."
                });
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Thank you for your feedback.";
            return RedirectToAction(nameof(SubmitFeedback));
        }

        public Task<IActionResult> Notifications() => TenantViewAsync();

        public Task<IActionResult> Profile() => TenantViewAsync();

        public Task<IActionResult> ChangeAvatar() => TenantViewAsync();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(52_428_800)]
        public async Task<IActionResult> UpdateAvatar(IFormFile? avatarFile, string? avatarChoice)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var (succeeded, errorMessage) = await _avatarService.UpdateAsync(
                currentUser,
                avatarFile,
                avatarChoice,
                HttpContext.RequestAborted);

            if (!succeeded)
            {
                ModelState.AddModelError(string.Empty, errorMessage ?? "Unable to update avatar.");
                return View("~/Areas/Tenant/Views/ChangeAvatar.cshtml", await BuildTenantModelAsync());
            }

            TempData["SuccessMessage"] = "Avatar updated successfully.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAvatar()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            await _avatarService.RemoveAsync(currentUser, HttpContext.RequestAborted);
            TempData["SuccessMessage"] = "Avatar removed successfully.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Marks one tenant notification read and optionally redirects to the related request.
        public async Task<IActionResult> MarkNotificationRead(int id, int? requestId)
        {
            var tenant = await GetCurrentUserAsync();
            if (tenant == null)
            {
                return Challenge();
            }

            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == tenant.Id);
            if (notification == null)
            {
                return NotFound();
            }

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return requestId.HasValue
                ? RedirectToAction(nameof(RequestDetail), new { id = requestId.Value })
                : RedirectToAction(nameof(Notifications));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Clears all unread notifications for the signed-in tenant.
        public async Task<IActionResult> MarkAllNotificationsRead()
        {
            var tenant = await GetCurrentUserAsync();
            if (tenant == null)
            {
                return Challenge();
            }

            var notifications = await _context.Notifications
                .Where(n => n.UserId == tenant.Id && !n.IsRead)
                .ToListAsync();

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Notifications marked as read.";
            return RedirectToAction(nameof(Notifications));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Updates tenant account details and optionally changes the password.
        public async Task<IActionResult> Profile(TenantProfileUpdateViewModel input)
        {
            var tenant = await GetCurrentUserAsync();
            if (tenant == null)
            {
                return Challenge();
            }

            var email = (input.Email ?? string.Empty).Trim();
            input.FullName = (input.FullName ?? string.Empty).Trim();
            input.Email = email;

            if (!string.Equals(tenant.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                var existingUser = await _userManager.FindByEmailAsync(email);
                if (existingUser != null && existingUser.Id != tenant.Id)
                {
                    ModelState.AddModelError(nameof(input.Email), "This email address is already in use.");
                }
            }

            var wantsPasswordChange =
                !string.IsNullOrWhiteSpace(input.CurrentPassword) ||
                !string.IsNullOrWhiteSpace(input.NewPassword) ||
                !string.IsNullOrWhiteSpace(input.ConfirmPassword);

            if (wantsPasswordChange)
            {
                if (string.IsNullOrWhiteSpace(input.CurrentPassword))
                {
                    ModelState.AddModelError(nameof(input.CurrentPassword), "Enter your current password to change your password.");
                }

                if (string.IsNullOrWhiteSpace(input.NewPassword))
                {
                    ModelState.AddModelError(nameof(input.NewPassword), "Enter a new password.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View("~/Areas/Tenant/Views/Profile.cshtml", await BuildTenantModelAsync());
            }

            tenant.FullName = input.FullName;
            tenant.Email = email;
            tenant.UserName = email;
            tenant.NormalizedEmail = _userManager.NormalizeEmail(email);
            tenant.NormalizedUserName = _userManager.NormalizeName(email);
            tenant.PhoneNumber = input.PhoneNumber;

            var result = await _userManager.UpdateAsync(tenant);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View("~/Areas/Tenant/Views/Profile.cshtml", await BuildTenantModelAsync());
            }

            if (wantsPasswordChange)
            {
                var passwordResult = await _userManager.ChangePasswordAsync(tenant, input.CurrentPassword!, input.NewPassword!);
                if (!passwordResult.Succeeded)
                {
                    foreach (var error in passwordResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }

                    return View("~/Areas/Tenant/Views/Profile.cshtml", await BuildTenantModelAsync());
                }
            }

            await _signInManager.RefreshSignInAsync(tenant);

            TempData["SuccessMessage"] = wantsPasswordChange
                ? "Profile and password updated successfully."
                : "Profile updated successfully.";
            return RedirectToAction(nameof(Profile));
        }

        private async Task<IActionResult> TenantViewAsync([CallerMemberName] string actionName = "")
        {
            var model = await BuildTenantModelAsync();
            return View($"~/Areas/Tenant/Views/{actionName}.cshtml", model);
        }

        // Centralizes tenant page data loading, including the selected request when a detail page needs it.
        private async Task<AreaDashboardViewModel> BuildTenantModelAsync(int? selectedRequestId = null)
        {
            var tenant = await GetCurrentUserAsync();
            return await _dashboardData.GetTenantDashboardAsync(tenant?.Id, selectedRequestId);
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        private Task<ApplicationUser?> GetCurrentUserAsync() => _userManager.GetUserAsync(User);

        // Loads the signed-in tenant profile with unit, property, and company context.
        private async Task<TenantProfile?> GetCurrentTenantProfileAsync()
        {
            return await _context.TenantProfiles
                .Include(t => t.User)
                .Include(t => t.Unit)
                    .ThenInclude(u => u.Property)
                .FirstOrDefaultAsync(t => t.UserId == CurrentUserId);
        }

        // Ensures tenants can mutate only their own maintenance requests.
        private async Task<MaintenanceRequest?> GetTenantRequestEntityAsync(int requestId, int tenantId)
        {
            return await _context.MaintenanceRequests
                .Include(r => r.Assignment)
                .FirstOrDefaultAsync(r => r.RequestId == requestId && r.TenantId == tenantId);
        }

        // Returns request IDs that already have feedback so the UI can hide duplicate feedback options.
        private async Task<IReadOnlyList<int>> GetFeedbackSubmittedRequestIdsAsync()
        {
            var tenant = await GetCurrentTenantProfileAsync();
            if (tenant == null)
            {
                return Array.Empty<int>();
            }

            return await _context.RequestFeedbacks
                .Where(f => f.TenantId == tenant.TenantId)
                .Select(f => f.RequestId)
                .ToListAsync();
        }

        // Finds managers who should receive notifications for a property-level tenant request.
        private async Task<List<string>> GetManagerUserIdsForPropertyAsync(int propertyId, int? companyId)
        {
            if (!companyId.HasValue)
            {
                return new List<string>();
            }

            return await _context.ManagerProfiles
                .Include(m => m.User)
                .Where(m => m.User.IsActive && m.User.CompanyId == companyId.Value)
                .Where(m => m.PropertyId == null || m.PropertyId == propertyId)
                .Select(m => m.UserId)
                .Distinct()
                .ToListAsync();
        }

        // Queues notifications for every manager responsible for the request's property.
        private async Task NotifyManagersAsync(int propertyId, int companyId, int requestId, string title, string message)
        {
            if (companyId == 0)
            {
                return;
            }

            foreach (var managerId in await GetManagerUserIdsForPropertyAsync(propertyId, companyId))
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = managerId,
                    RequestId = requestId,
                    Title = title,
                    Message = message
                });
            }
        }

        // Recomputes a technician's average rating after feedback is submitted.
        private async Task RecalculateTechnicianRatingAsync(int technicianId)
        {
            var ratings = await _context.RequestFeedbacks
                .Where(f => f.TechnicianId == technicianId)
                .Select(f => f.Rating)
                .ToListAsync();

            var technician = await _context.TechnicianProfiles.FindAsync(technicianId);
            if (technician == null)
            {
                return;
            }

            technician.AverageRating = ratings.Count == 0 ? 0 : (decimal)ratings.Average();
            await _context.SaveChangesAsync();
        }

        private void ValidateCategory(string category)
        {
            if (!RequestCategories.Contains(category))
            {
                ModelState.AddModelError(nameof(TenantMaintenanceRequestViewModel.Category), "Choose a valid category.");
            }
        }

        // Validates tenant issue photo uploads before saving anything to disk.
        private void ValidateIssuePhotoFiles(IEnumerable<IFormFile>? files, string fieldName, bool required)
        {
            var uploads = files?.Where(f => f.Length > 0).ToList() ?? new List<IFormFile>();
            if (required && !uploads.Any())
            {
                ModelState.AddModelError(fieldName, "Upload at least one issue photo.");
                return;
            }

            foreach (var file in uploads)
            {
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedImageExtensions.Contains(extension))
                {
                    ModelState.AddModelError(fieldName, "Only JPG, PNG, GIF, or WebP files are allowed.");
                }

                if (!string.IsNullOrWhiteSpace(file.ContentType) &&
                    !AllowedImageMimeTypes.Contains(file.ContentType.Trim()))
                {
                    ModelState.AddModelError(fieldName, "Only JPG, PNG, GIF, or WebP files are allowed.");
                }

                if (file.Length > 10 * 1024 * 1024)
                {
                    ModelState.AddModelError(fieldName, "Each image must be 10MB or smaller.");
                }
            }
        }

        // Saves issue photos to private S3 storage and creates request image records plus manager notifications.
        private async Task SaveIssuePhotosAsync(
            MaintenanceRequest request,
            TenantProfile tenant,
            IReadOnlyList<IFormFile> uploads)
        {
            foreach (var file in uploads)
            {
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var fileName = $"{CurrentUserId}-{request.RequestId}-{Guid.NewGuid():N}{extension}";

                var imageUrl = await _fileStorage.UploadAsync(
                    file,
                    "issues",
                    fileName,
                    HttpContext.RequestAborted);

                _context.RequestImages.Add(new RequestImage
                {
                    RequestId = request.RequestId,
                    UploadedByUserId = CurrentUserId,
                    ImageUrl = imageUrl,
                    ImageType = "IssuePhoto",
                    UploadedAt = DateTime.UtcNow
                });
            }

            var managerIds = await GetManagerUserIdsForPropertyAsync(
                request.PropertyId,
                tenant.User.CompanyId);

            foreach (var managerId in managerIds)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = managerId,
                    RequestId = request.RequestId,
                    Title = "Issue photos uploaded",
                    Message =
                        $"{tenant.User.FullName} uploaded issue photos for {FormatRequestId(request.RequestId)}."
                });
            }
        }

        private static string FormatRequestId(int requestId) => $"REQ-{requestId:0000}";
    }
}
