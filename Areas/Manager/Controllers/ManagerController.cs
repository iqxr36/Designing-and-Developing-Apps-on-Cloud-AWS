using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.CompilerServices;
using CloudMVCApplication.Data;
using CloudMVCApplication.Models;
using CloudMVCApplication.Services;
using CloudMVCApplication.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CloudMVCApplication.Areas.Manager.Controllers
{
    [Authorize(Roles = "Manager")]
    [Area("Manager")]
    [Route("[area]/[action]")]
    public class ManagerController : Controller
    {
        private static readonly string[] RequestStatuses = { "Pending", "Assigned", "In Progress", "Completed", "Cancelled" };
        private static readonly string[] RequestPriorities = { "Low", "Medium", "High", "Urgent" };

        private readonly ApplicationDbContext _context;
        private readonly DashboardDataService _dashboardData;
        private readonly MessagingService _messagingService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly TechnicianPaymentService _technicianPaymentService;
        private readonly TechnicianWorkloadService _workloadService;
        private readonly IAvatarService _avatarService;

        public ManagerController(
            ApplicationDbContext context,
            DashboardDataService dashboardData,
            MessagingService messagingService,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            TechnicianPaymentService technicianPaymentService,
            TechnicianWorkloadService workloadService,
            IAvatarService avatarService)
        {
            _context = context;
            _dashboardData = dashboardData;
            _messagingService = messagingService;
            _userManager = userManager;
            _signInManager = signInManager;
            _technicianPaymentService = technicianPaymentService;
            _workloadService = workloadService;
            _avatarService = avatarService;
        }

        public Task<IActionResult> Dashboard() => ManagerViewAsync();

        // Lists the manager's maintenance requests and applies simple in-memory filters for the current page.
        public async Task<IActionResult> MaintenanceRequests(string? search, string? status, string? priority, string? property)
        {
            var model = await BuildManagerModelAsync();
            var requests = model.Requests.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                requests = requests.Where(r =>
                    r.DisplayId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    r.TenantName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    r.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    r.PropertyName.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                requests = requests.Where(r => r.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(priority))
            {
                requests = requests.Where(r => r.Priority == priority);
            }

            if (!string.IsNullOrWhiteSpace(property))
            {
                requests = requests.Where(r => r.PropertyName == property);
            }

            model.Requests = requests.ToList();
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Priority = priority;
            ViewBag.Property = property;
            return View("~/Areas/Manager/Views/MaintenanceRequests.cshtml", model);
        }

        [HttpGet]
        // Loads one request detail view, including payment readiness data when the job is completed.
        public async Task<IActionResult> RequestDetails(int id)
        {
            var manager = await GetCurrentManagerAsync();
            var model = await BuildManagerModelAsync(id);
            if (model.SelectedRequest == null)
            {
                return NotFound();
            }

            model.SelectedRequestPayment = await _technicianPaymentService.BuildPaymentReleaseViewModelAsync(
                id,
                manager?.CompanyId);
            return View("~/Areas/Manager/Views/RequestDetails.cshtml", model);
        }

        [HttpGet]
        // Opens the dispatch screen for a request that belongs to the manager's company portfolio.
        public async Task<IActionResult> AssignTechnician(int id)
        {
            if (id <= 0)
            {
                return RedirectToAction(nameof(MaintenanceRequests));
            }

            var model = await BuildManagerModelAsync(id);
            if (model.SelectedRequest == null)
            {
                TempData["ErrorMessage"] = "Request not found or not in your portfolio.";
                return RedirectToAction(nameof(MaintenanceRequests));
            }

            return View("~/Areas/Manager/Views/AssignTechnician.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Assigns or reassigns a technician, updates workload state, writes timeline history, and notifies both technician and tenant.
        public async Task<IActionResult> AssignTechnician(int? id, ManagerAssignmentViewModel input)
        {
            if (input.RequestId == 0 && id.HasValue)
            {
                input.RequestId = id.Value;
            }

            ValidatePriority(input.Priority);

            var manager = await GetCurrentManagerAsync();
            if (manager == null)
            {
                return Challenge();
            }

            var request = await GetManagedRequestAsync(input.RequestId, manager);
            if (request == null)
            {
                TempData["ErrorMessage"] = "Request not found or not in your portfolio.";
                return RedirectToAction(nameof(MaintenanceRequests));
            }

            var technician = await _context.TechnicianProfiles
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TechnicianId == input.TechnicianId && t.User.IsActive);
            if (technician == null)
            {
                ModelState.AddModelError(nameof(input.TechnicianId), "Choose an active technician.");
            }

            if (!ModelState.IsValid || technician == null)
            {
                TempData["ErrorMessage"] = "Assignment was not saved. Please choose an active technician and try again.";
                var model = await BuildManagerModelAsync(input.RequestId);
                return View("~/Areas/Manager/Views/AssignTechnician.cshtml", model);
            }

            var activeAssignments = await _context.Assignments
                .Include(a => a.Request)
                .Where(a => a.TechnicianId == technician.TechnicianId && a.RequestId != request.RequestId)
                .ToListAsync();
            var workload = _workloadService.CreateSnapshot(activeAssignments.Count(_workloadService.IsActiveAssignment));
            if (workload.WorkloadPercentage >= 100)
            {
                TempData["ErrorMessage"] = $"{technician.User.FullName} is at full capacity ({workload.ActiveJobs}/{workload.MaxActiveJobs} active jobs) and cannot be assigned another job.";
                var model = await BuildManagerModelAsync(input.RequestId);
                return View("~/Areas/Manager/Views/AssignTechnician.cshtml", model);
            }

            var oldStatus = request.Status;
            var previousTechnician = request.Assignment?.Technician;
            request.Priority = input.Priority;
            request.Status = "Assigned";
            request.UpdatedAt = DateTime.UtcNow;

            if (request.Assignment == null)
            {
                _context.Assignments.Add(new Assignment
                {
                    RequestId = request.RequestId,
                    TechnicianId = technician.TechnicianId,
                    AssignedByManagerId = manager.Id,
                    AssignedDate = DateTime.UtcNow,
                    Status = "Assigned",
                    CompletionNotes = input.Notes
                });
            }
            else
            {
                request.Assignment.TechnicianId = technician.TechnicianId;
                request.Assignment.AssignedByManagerId = manager.Id;
                request.Assignment.AssignedDate = DateTime.UtcNow;
                request.Assignment.Status = "Assigned";
                request.Assignment.CompletedAt = null;
                request.Assignment.CompletionNotes = input.Notes;
            }

            technician.AvailabilityStatus = "On Job";
            if (previousTechnician != null && previousTechnician.TechnicianId != technician.TechnicianId)
            {
                var hasOtherActiveAssignments = await _context.Assignments.AnyAsync(a =>
                    a.TechnicianId == previousTechnician.TechnicianId &&
                    a.AssignmentId != request.Assignment!.AssignmentId &&
                    a.Status != "Completed" &&
                    a.Status != "Cancelled");
                previousTechnician.AvailabilityStatus = hasOtherActiveAssignments ? "On Job" : "Available";
            }

            AddRequestHistory(request.RequestId, manager.Id, oldStatus, request.Status, input.Notes ?? $"Assigned to {technician.User.FullName}.");
            AddNotification(technician.UserId, request.RequestId, "Maintenance request assigned", $"You are assigned to {FormatRequestId(request.RequestId)}.");
            AddNotification(request.Tenant.UserId, request.RequestId, "Maintenance request assigned", $"{FormatRequestId(request.RequestId)} has been assigned to a technician.");

            await _context.SaveChangesAsync();
            await _messagingService.CreateJobCoordinationConversationAsync(request.RequestId, manager.Id, technician.TechnicianId);
            await AddAuditLogAsync(manager.Id, "AssignedRequest", nameof(MaintenanceRequest), request.RequestId.ToString(), $"Assigned {FormatRequestId(request.RequestId)} to {technician.User.FullName}.");

            TempData["SuccessMessage"] = "Technician assigned successfully.";
            return RedirectToAction(nameof(RequestDetails), new { id = request.RequestId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Updates request status/priority and keeps the linked assignment, technician availability, timeline, and notifications in sync.
        public async Task<IActionResult> UpdateRequest(ManagerRequestUpdateViewModel input)
        {
            ValidateStatus(input.Status);
            ValidatePriority(input.Priority);

            var manager = await GetCurrentManagerAsync();
            if (manager == null)
            {
                return Challenge();
            }

            var request = await GetManagedRequestAsync(input.RequestId, manager);
            if (request == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                var model = await BuildManagerModelAsync(input.RequestId);
                return View("~/Areas/Manager/Views/RequestDetails.cshtml", model);
            }

            var oldStatus = request.Status;
            var oldPriority = request.Priority;
            request.Status = input.Status;
            request.Priority = input.Priority;
            request.UpdatedAt = DateTime.UtcNow;

            if (request.Assignment != null)
            {
                request.Assignment.Status = input.Status;
                request.Assignment.CompletedAt = input.Status == "Completed" ? DateTime.UtcNow : request.Assignment.CompletedAt;

                if (input.Status == "Completed")
                {
                    request.Assignment.Technician.TotalCompletedJobs += oldStatus == "Completed" ? 0 : 1;
                }

                var isClosedStatus = input.Status == "Completed" || input.Status == "Cancelled";
                var hasOtherActiveAssignments = await _context.Assignments.AnyAsync(a =>
                    a.TechnicianId == request.Assignment.TechnicianId &&
                    a.AssignmentId != request.Assignment.AssignmentId &&
                    a.Status != "Completed" &&
                    a.Status != "Cancelled");
                request.Assignment.Technician.AvailabilityStatus = isClosedStatus && !hasOtherActiveAssignments ? "Available" : "On Job";
            }

            var notes = input.Notes ?? $"Manager updated status from {oldStatus} to {request.Status}.";
            AddRequestHistory(request.RequestId, manager.Id, oldStatus, request.Status, notes);
            AddNotification(request.Tenant.UserId, request.RequestId, "Maintenance request updated", $"{FormatRequestId(request.RequestId)} status changed to {request.Status}.");

            if (request.Assignment != null)
            {
                AddNotification(request.Assignment.Technician.UserId, request.RequestId, "Maintenance request updated", $"{FormatRequestId(request.RequestId)} status changed to {request.Status}.");
            }

            await _context.SaveChangesAsync();
            await AddAuditLogAsync(manager.Id, "UpdatedRequest", nameof(MaintenanceRequest), request.RequestId.ToString(), $"Updated {FormatRequestId(request.RequestId)} from {oldStatus}/{oldPriority} to {request.Status}/{request.Priority}.");

            TempData["SuccessMessage"] = "Request updated successfully.";
            return RedirectToAction(nameof(RequestDetails), new { id = request.RequestId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Creates the technician payment invoice after completion and records the payment attempt in the request timeline.
        public async Task<IActionResult> ReleasePayment(ManagerReleasePaymentViewModel input)
        {
            var manager = await GetCurrentManagerAsync();
            if (manager == null)
            {
                return Challenge();
            }

            var request = await GetManagedRequestAsync(input.RequestId, manager);
            if (request == null)
            {
                TempData["ErrorMessage"] = "Request not found or not in your portfolio.";
                return RedirectToAction(nameof(MaintenanceRequests));
            }

            if (request.Status != "Completed")
            {
                TempData["ErrorMessage"] = "Payment can only be released for completed jobs.";
                return RedirectToAction(nameof(RequestDetails), new { id = input.RequestId });
            }

            var successUrl = Url.Action(nameof(RequestDetails), "Manager", new { area = "Manager", id = input.RequestId, payment = "pending" }, Request.Scheme)!;
            var cancelUrl = Url.Action(nameof(RequestDetails), "Manager", new { area = "Manager", id = input.RequestId, payment = "cancelled" }, Request.Scheme)!;

            var (payment, checkoutUrl, createError) = await _technicianPaymentService.CreateXenditPaymentAsync(
                input.RequestId,
                input.Amount,
                input.PaymentMethod,
                input.Notes,
                manager.Id,
                manager.CompanyId,
                manager.Email ?? string.Empty,
                successUrl,
                cancelUrl);

            if (createError != null || payment == null)
            {
                TempData["ErrorMessage"] = createError ?? "Could not create payment.";
                return RedirectToAction(nameof(RequestDetails), new { id = input.RequestId });
            }

            _context.RequestStatusHistories.Add(new RequestStatusHistory
            {
                RequestId = request.RequestId,
                ChangedByUserId = manager.Id,
                OldStatus = request.Status,
                NewStatus = request.Status,
                Notes = $"Technician payment invoice created for RM {payment.GrossAmount:0.00}. Technician receives RM {payment.TechnicianNetAmount:0.00} after commission.",
                ChangedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Technician payment invoice created. Commission RM {payment.PlatformCommissionAmount:0.00}; technician receives RM {payment.TechnicianNetAmount:0.00}.";
            await AddAuditLogAsync(
                manager.Id,
                "CreatedTechnicianPaymentInvoice",
                nameof(TechnicianPayment),
                payment.Id.ToString(),
                $"Created technician payment invoice of RM {payment.GrossAmount:0.00} for {FormatRequestId(request.RequestId)}.");

            if (!string.IsNullOrWhiteSpace(checkoutUrl))
            {
                return Redirect(checkoutUrl);
            }

            return RedirectToAction(nameof(RequestDetails), new { id = input.RequestId });
        }

        public Task<IActionResult> TechnicianWorkload() => ManagerViewAsync();

        public Task<IActionResult> CompletedRepairs() => ManagerViewAsync();

        public Task<IActionResult> Reports() => ManagerViewAsync();

        [HttpGet]
        // Builds the manager's technician payment report from completed Xendit/payment records.
        public async Task<IActionResult> TechnicianPayments()
        {
            var manager = await GetCurrentManagerAsync();
            if (manager == null)
            {
                return Challenge();
            }

            var payments = await _context.TechnicianPayments
                .Include(p => p.MaintenanceRequest)
                .Include(p => p.Manager)
                .Include(p => p.Technician)
                    .ThenInclude(t => t.User)
                .Include(p => p.Company)
                .Where(p => p.ManagerId == manager.Id)
                .Where(p => !manager.CompanyId.HasValue || p.CompanyId == manager.CompanyId.Value)
                .OrderByDescending(p => p.PaidAt ?? p.CreatedAt)
                .Select(p => new TechnicianPaymentReportRowViewModel(
                    p.Id,
                    p.MaintenanceRequestId,
                    $"REQ-{p.MaintenanceRequestId:0000}",
                    p.MaintenanceRequest.Title,
                    p.Manager.FullName,
                    p.Technician.User.FullName,
                    p.Company.CompanyName,
                    p.GrossAmount,
                    p.PlatformCommissionAmount,
                    p.TechnicianNetAmount,
                    p.PaymentStatus,
                    p.PaidAt,
                    p.TransactionReference))
                .ToListAsync();

            return View("~/Areas/Manager/Views/TechnicianPayments.cshtml", new TechnicianPaymentReportViewModel
            {
                Payments = payments,
                TotalGrossAmount = payments.Sum(p => p.GrossAmount),
                TotalPlatformCommission = payments.Sum(p => p.PlatformCommissionAmount),
                TotalTechnicianNetAmount = payments.Sum(p => p.TechnicianNetAmount),
                PaymentCount = payments.Count
            });
        }

        public Task<IActionResult> Notifications() => ManagerViewAsync();

        // Shows manager conversations after ensuring tenant support conversation membership is up to date.
        public async Task<IActionResult> Messages()
        {
            var manager = await GetCurrentManagerAsync();
            if (manager == null)
            {
                return Challenge();
            }

            await _messagingService.SyncAllTenantSupportParticipantsAsync();
            return View("~/Areas/Manager/Views/Messages.cshtml", await _messagingService.GetInboxAsync(manager.Id, "Manager"));
        }

        [HttpGet]
        // Opens a conversation only if the manager participates in it, then marks messages as read.
        public async Task<IActionResult> Chat(int id)
        {
            var manager = await GetCurrentManagerAsync();
            if (manager == null)
            {
                return Challenge();
            }

            var chat = await _messagingService.GetConversationAsync(id, manager.Id);
            if (chat == null)
            {
                return Forbid();
            }

            await _messagingService.MarkAsReadAsync(id, manager.Id);
            return View("~/Areas/Manager/Views/Chat.cshtml", chat);
        }

        // Redirects from a request to the correct tenant-support or job-coordination chat thread.
        public async Task<IActionResult> ChatByRequest(int requestId, ConversationType type = ConversationType.TenantSupport)
        {
            var manager = await GetCurrentManagerAsync();
            if (manager == null)
            {
                return Challenge();
            }

            var conversationId = await _messagingService.GetConversationIdByRequestAsync(requestId, type, manager.Id);
            if (!conversationId.HasValue)
            {
                return NotFound();
            }

            return RedirectToAction(nameof(Chat), new { id = conversationId.Value });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(int id, string body)
        {
            var manager = await GetCurrentManagerAsync();
            if (manager == null)
            {
                return Challenge();
            }

            await _messagingService.SendMessageAsync(id, manager.Id, body);
            return RedirectToAction(nameof(Chat), new { id });
        }

        public Task<IActionResult> Profile() => ManagerViewAsync();

        public Task<IActionResult> ChangeAvatar() => ManagerViewAsync();

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
                return View("~/Areas/Manager/Views/ChangeAvatar.cshtml", await _dashboardData.GetManagerDashboardAsync(currentUser.Id));
            }

            await AddAuditLogAsync(currentUser.Id, "UpdatedAvatar", nameof(ApplicationUser), currentUser.Id, "Manager updated their avatar.");
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
            await AddAuditLogAsync(currentUser.Id, "RemovedAvatar", nameof(ApplicationUser), currentUser.Id, "Manager removed their avatar.");
            TempData["SuccessMessage"] = "Avatar removed successfully.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Marks a single notification as read; request-linked notifications can jump straight to request details.
        public async Task<IActionResult> MarkNotificationRead(int id, int? requestId)
        {
            var manager = await GetCurrentManagerAsync();
            if (manager == null)
            {
                return Challenge();
            }

            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == manager.Id);
            if (notification == null)
            {
                return NotFound();
            }

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return requestId.HasValue
                ? RedirectToAction(nameof(RequestDetails), new { id = requestId.Value })
                : RedirectToAction(nameof(Notifications));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Clears all unread notifications for the signed-in manager.
        public async Task<IActionResult> MarkAllNotificationsRead()
        {
            var manager = await GetCurrentManagerAsync();
            if (manager == null)
            {
                return Challenge();
            }

            var notifications = await _context.Notifications
                .Where(n => n.UserId == manager.Id && !n.IsRead)
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
        // Updates manager account details and optionally changes the password in the same form submission.
        public async Task<IActionResult> Profile(ManagerProfileUpdateViewModel input)
        {
            var manager = await GetCurrentManagerAsync();
            if (manager == null)
            {
                return Challenge();
            }

            var email = (input.Email ?? string.Empty).Trim();
            input.FullName = (input.FullName ?? string.Empty).Trim();
            input.Email = email;

            if (!string.Equals(manager.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                var existingUser = await _userManager.FindByEmailAsync(email);
                if (existingUser != null && existingUser.Id != manager.Id)
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
                var model = await BuildManagerModelAsync();
                return View("~/Areas/Manager/Views/Profile.cshtml", model);
            }

            manager.FullName = input.FullName;
            manager.Email = email;
            manager.UserName = email;
            manager.NormalizedEmail = _userManager.NormalizeEmail(email);
            manager.NormalizedUserName = _userManager.NormalizeName(email);
            manager.PhoneNumber = input.PhoneNumber;

            var result = await _userManager.UpdateAsync(manager);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                var model = await BuildManagerModelAsync();
                return View("~/Areas/Manager/Views/Profile.cshtml", model);
            }

            if (wantsPasswordChange)
            {
                var passwordResult = await _userManager.ChangePasswordAsync(manager, input.CurrentPassword!, input.NewPassword!);
                if (!passwordResult.Succeeded)
                {
                    foreach (var error in passwordResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }

                    var model = await BuildManagerModelAsync();
                    return View("~/Areas/Manager/Views/Profile.cshtml", model);
                }
            }

            await _signInManager.RefreshSignInAsync(manager);

            await AddAuditLogAsync(manager.Id, "UpdatedProfile", nameof(ApplicationUser), manager.Id, "Manager updated their profile.");
            TempData["SuccessMessage"] = wantsPasswordChange
                ? "Profile and password updated successfully."
                : "Profile updated successfully.";
            return RedirectToAction(nameof(Profile));
        }

        private async Task<IActionResult> ManagerViewAsync([CallerMemberName] string actionName = "")
        {
            var model = await BuildManagerModelAsync();
            return View($"~/Areas/Manager/Views/{actionName}.cshtml", model);
        }

        // Centralizes manager dashboard/view model loading so all manager pages share the same data shape.
        private async Task<AreaDashboardViewModel> BuildManagerModelAsync(int? selectedRequestId = null)
        {
            var manager = await GetCurrentManagerAsync();
            return await _dashboardData.GetManagerDashboardAsync(manager?.Id, selectedRequestId);
        }

        private Task<ApplicationUser?> GetCurrentManagerAsync()
        {
            return _userManager.GetUserAsync(User);
        }

        // Enforces portfolio ownership before manager actions can read or mutate a request.
        private async Task<MaintenanceRequest?> GetManagedRequestAsync(int requestId, ApplicationUser manager)
        {
            var request = await _context.MaintenanceRequests
                .Include(r => r.Tenant)
                .Include(r => r.Property)
                .Include(r => r.Assignment)
                    .ThenInclude(a => a!.Technician)
                        .ThenInclude(t => t.User)
                .FirstOrDefaultAsync(r => r.RequestId == requestId);

            if (request == null)
            {
                return null;
            }

            if (manager.CompanyId.HasValue && request.Property.CompanyId != manager.CompanyId.Value)
            {
                return null;
            }

            return request;
        }

        private void ValidateStatus(string status)
        {
            if (!RequestStatuses.Contains(status))
            {
                ModelState.AddModelError(nameof(ManagerRequestUpdateViewModel.Status), "Choose a valid request status.");
            }
        }

        private void ValidatePriority(string priority)
        {
            if (!RequestPriorities.Contains(priority))
            {
                ModelState.AddModelError(nameof(ManagerAssignmentViewModel.Priority), "Choose a valid priority.");
            }
        }

        // Appends one request timeline entry; SaveChanges is left to the caller's transaction flow.
        private void AddRequestHistory(int requestId, string userId, string? oldStatus, string? newStatus, string? notes)
        {
            _context.RequestStatusHistories.Add(new RequestStatusHistory
            {
                RequestId = requestId,
                ChangedByUserId = userId,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                Notes = notes,
                ChangedAt = DateTime.UtcNow
            });
        }

        // Queues a request-linked notification for another user; SaveChanges is left to the caller.
        private void AddNotification(string userId, int requestId, string title, string message)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                RequestId = requestId,
                Title = title,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Persists an audit trail entry for important manager actions.
        private async Task AddAuditLogAsync(string userId, string action, string entityName, string entityId, string description)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                Description = description,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }

        private static string FormatRequestId(int requestId) => $"REQ-{requestId:0000}";
    }
}
