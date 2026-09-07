using CloudMVCApplication.Models;
using CloudMVCApplication.Data;
using CloudMVCApplication.Services;
using CloudMVCApplication.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CloudMVCApplication.Areas.SuperAdmin.Controllers
{
    [Area("SuperAdmin")]
    [Authorize(Roles = "SuperAdmin")]
    [Route("[area]/[action]")]
    public class SuperAdminController : Controller
    {
        private readonly PlatformAdminService _platformAdmin;
        private readonly XenditSubscriptionService _xenditSubscription;
        private readonly TechnicianPayoutService _payoutService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IFileStorageService _fileStorage;
        private readonly IAvatarService _avatarService;
        private readonly IEmailService _emailService;
        private readonly AppSettings _appSettings;
        private readonly ILogger<SuperAdminController> _logger;

        public SuperAdminController(
            PlatformAdminService platformAdmin,
            XenditSubscriptionService xenditSubscription,
            TechnicianPayoutService payoutService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IFileStorageService fileStorage,
            IAvatarService avatarService,
            IEmailService emailService,
            IOptions<AppSettings> appSettings,
            ILogger<SuperAdminController> logger)
        {
            _platformAdmin = platformAdmin;
            _xenditSubscription = xenditSubscription;
            _payoutService = payoutService;
            _userManager = userManager;
            _context = context;
            _fileStorage = fileStorage;
            _avatarService = avatarService;
            _emailService = emailService;
            _appSettings = appSettings.Value;
            _logger = logger;
        }

        public async Task<IActionResult> Dashboard()
        {
            ViewData["Title"] = "Platform Dashboard";
            return View(await _platformAdmin.GetDashboardAsync());
        }

        public async Task<IActionResult> Profile()
        {
            ViewData["Title"] = "My Profile";
            var model = await BuildProfileModelAsync();
            if (model == null)
            {
                return Challenge();
            }

            return View(model);
        }

        public async Task<IActionResult> ChangeAvatar()
        {
            ViewData["Title"] = "Change Avatar";
            var model = await BuildProfileModelAsync();
            if (model == null)
            {
                return Challenge();
            }

            return View(model);
        }

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
                ViewData["Title"] = "Change Avatar";
                return View("ChangeAvatar", await BuildProfileModelAsync() ?? new AreaDashboardViewModel());
            }

            await LogActionAsync(
                "UpdatedAvatar",
                nameof(ApplicationUser),
                currentUser.Id,
                "Updated Super Admin avatar.");
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
            await LogActionAsync(
                "RemovedAvatar",
                nameof(ApplicationUser),
                currentUser.Id,
                "Removed Super Admin avatar.");
            TempData["SuccessMessage"] = "Avatar removed successfully.";
            return RedirectToAction(nameof(Profile));
        }

        public async Task<IActionResult> Companies(string? search, string? plan, string? status, bool includeDeleted = false)
        {
            ViewData["Title"] = "Companies";
            ViewBag.Search = search;
            ViewBag.Plan = plan;
            ViewBag.Status = status;
            ViewBag.IncludeDeleted = includeDeleted;
            return View(await _platformAdmin.GetCompaniesAsync(search, plan, status, includeDeleted));
        }

        public async Task<IActionResult> CompanyDetail(int id)
        {
            var model = await _platformAdmin.GetCompanyDetailAsync(id);
            if (model == null)
            {
                return NotFound();
            }

            ViewData["Title"] = model.CompanyName;
            return View(model);
        }

        [HttpGet]
        public IActionResult CreateCompany()
        {
            ViewData["Title"] = "Add Company";
            return View(new SuperAdminCreateCompanyViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCompany(SuperAdminCreateCompanyViewModel model)
        {
            ViewData["Title"] = "Add Company";

            if (!SubscriptionPlans.All.Contains(model.Plan))
            {
                ModelState.AddModelError(nameof(model.Plan), "Select a valid subscription plan.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var (succeeded, companyId, errors) = await _platformAdmin.CreateCompanyAsync(model);
            if (!succeeded)
            {
                foreach (var error in errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                return View(model);
            }

            await LogActionAsync(
                "CreatedCompany",
                nameof(PropertyManagementCompany),
                companyId.ToString(),
                $"Created company {model.CompanyName} ({model.CompanyEmail}).");

            TempData["SuccessMessage"] = "Company created successfully.";
            return RedirectToAction(nameof(CompanyDetail), new { id = companyId });
        }

        [HttpGet]
        public async Task<IActionResult> EditCompany(int id)
        {
            var detail = await _platformAdmin.GetCompanyDetailAsync(id);
            if (detail == null)
            {
                return NotFound();
            }

            ViewData["Title"] = $"Edit {detail.CompanyName}";
            return View(new SuperAdminEditCompanyViewModel
            {
                CompanyId = detail.CompanyId,
                CompanyName = detail.CompanyName,
                CompanyEmail = detail.CompanyEmail,
                CompanyPhone = detail.CompanyPhone,
                CompanyAddress = detail.CompanyAddress
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCompany(SuperAdminEditCompanyViewModel model)
        {
            ViewData["Title"] = "Edit Company";

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var (succeeded, errors) = await _platformAdmin.UpdateCompanyAsync(model);
            if (!succeeded)
            {
                foreach (var error in errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                return View(model);
            }

            await LogActionAsync(
                "UpdatedCompany",
                nameof(PropertyManagementCompany),
                model.CompanyId.ToString(),
                $"Updated company profile for {model.CompanyName}.");

            TempData["SuccessMessage"] = "Company details updated.";
            return RedirectToAction(nameof(CompanyDetail), new { id = model.CompanyId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCompany(int id)
        {
            if (!await _platformAdmin.SoftDeleteCompanyAsync(id))
            {
                TempData["ErrorMessage"] = "Could not delete company.";
                return RedirectToAction(nameof(CompanyDetail), new { id });
            }

            await LogActionAsync(
                "DeletedCompany",
                nameof(PropertyManagementCompany),
                id.ToString(),
                $"Soft-deleted company {id} and deactivated related users.");

            TempData["SuccessMessage"] = "Company deleted (deactivated). Related data was kept.";
            return RedirectToAction(nameof(Companies));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSubscriptionStatus(int id, string status, DateTime? trialEndsAt)
        {
            if (!await _platformAdmin.UpdateSubscriptionStatusAsync(id, status, trialEndsAt))
            {
                TempData["ErrorMessage"] = "Could not update subscription status.";
            }
            else
            {
                await LogActionAsync("UpdatedSubscriptionStatus", nameof(PropertyManagementCompany), id.ToString(), $"Set subscription status to {status} for company {id}.");
                TempData["SuccessMessage"] = "Subscription status updated.";
            }

            return RedirectToAction(nameof(CompanyDetail), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuspendCompany(int id)
        {
            await _xenditSubscription.SuspendSubscriptionAsync(id);
            await LogActionAsync("SuspendedCompany", nameof(PropertyManagementCompany), id.ToString(), $"Suspended company {id}.");
            TempData["SuccessMessage"] = "Company suspended.";
            return RedirectToAction(nameof(CompanyDetail), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReactivateCompany(int id)
        {
            await _xenditSubscription.ReactivateCompanyAsync(id);
            await LogActionAsync("ReactivatedCompany", nameof(PropertyManagementCompany), id.ToString(), $"Reactivated company {id}.");
            TempData["SuccessMessage"] = "Company reactivated.";
            return RedirectToAction(nameof(CompanyDetail), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePlan(int id, string plan)
        {
            if (!await _xenditSubscription.ChangePlanAsync(id, plan))
            {
                TempData["ErrorMessage"] = "Could not change subscription plan.";
            }
            else
            {
                await LogActionAsync("ChangedSubscriptionPlan", nameof(PropertyManagementCompany), id.ToString(), $"Changed company {id} to plan {plan}.");
                TempData["SuccessMessage"] = "Subscription plan updated.";
            }

            return RedirectToAction(nameof(CompanyDetail), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkSubscriptionActive(int id)
        {
            await _xenditSubscription.MarkSubscriptionActiveManuallyAsync(id);
            await LogActionAsync("ManualSubscriptionActivation", nameof(PropertyManagementCompany), id.ToString(), $"Manually activated subscription for company {id}.");
            TempData["SuccessMessage"] = "Subscription marked active.";
            return RedirectToAction(nameof(CompanyDetail), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAdminActive(string userId, int companyId)
        {
            var admins = await _platformAdmin.GetCompanyAdminsAsync(companyId);
            var admin = admins.FirstOrDefault(a => a.UserId == userId);
            if (admin == null)
            {
                return NotFound();
            }

            await _platformAdmin.SetUserActiveAsync(userId, !admin.IsActive);
            await LogActionAsync(
                admin.IsActive ? "DeactivatedCompanyAdmin" : "ActivatedCompanyAdmin",
                nameof(ApplicationUser),
                userId,
                $"{(admin.IsActive ? "Deactivated" : "Activated")} company admin {admin.Email}.");

            TempData["SuccessMessage"] = admin.IsActive ? "Company admin deactivated." : "Company admin activated.";
            return RedirectToAction(nameof(CompanyDetail), new { id = companyId });
        }

        public async Task<IActionResult> Technicians(bool pendingOnly = false, string? reviewStatus = null)
        {
            ViewData["Title"] = pendingOnly ? "Pending Technicians" : "Technicians";
            ViewBag.PendingOnly = pendingOnly;
            ViewBag.ReviewStatus = reviewStatus ?? string.Empty;
            return View(await _platformAdmin.GetTechniciansAsync(pendingOnly, reviewStatus));
        }

        public async Task<IActionResult> TechnicianDetails(string userId)
        {
            var model = await _platformAdmin.GetTechnicianDetailAsync(userId);
            if (model == null)
            {
                return NotFound();
            }

            ViewData["Title"] = model.FullName;
            return View("~/Areas/SuperAdmin/Views/SuperAdmin/TechnicianDetails.cshtml", model);
        }

        [HttpGet]
        public IActionResult CreateTechnician()
        {
            ViewData["Title"] = "Add Technician";
            return View(new SuperAdminCreateTechnicianViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(52_428_800)]
        public async Task<IActionResult> CreateTechnician(SuperAdminCreateTechnicianViewModel model)
        {
            ViewData["Title"] = "Add Technician";
            ValidateTechnicianCertificates(model);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var (succeeded, userId, errors) = await _platformAdmin.CreateTechnicianAsync(model, _fileStorage);
            if (!succeeded || string.IsNullOrEmpty(userId))
            {
                foreach (var error in errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                return View(model);
            }

            await LogActionAsync(
                "CreatedTechnician",
                nameof(ApplicationUser),
                userId,
                $"Created technician {model.FullName} ({model.Email}).");

            await TrySendTechnicianEmailAsync(
                model.Email,
                TechnicianNotificationEmailBuilder.WelcomeSubject,
                TechnicianNotificationEmailBuilder.BuildWelcomeHtml(
                    model.FullName,
                    model.Email,
                    model.Password,
                    GetLoginUrl()));

            TempData["SuccessMessage"] = "Technician created and set to Available. Welcome email sent.";
            return RedirectToAction(nameof(TechnicianDetails), new { userId });
        }

        [HttpGet]
        public async Task<IActionResult> EditTechnician(string userId)
        {
            var detail = await _platformAdmin.GetTechnicianDetailAsync(userId);
            if (detail == null)
            {
                return NotFound();
            }

            ViewData["Title"] = $"Edit {detail.FullName}";
            return View(new SuperAdminEditTechnicianViewModel
            {
                UserId = detail.UserId,
                FullName = detail.FullName,
                Email = detail.Email,
                PhoneNumber = detail.PhoneNumber,
                Specialization = detail.Specialization,
                ExperienceYears = detail.ExperienceYears,
                ServiceArea = detail.ServiceArea ?? string.Empty,
                IsActive = detail.IsActive,
                AvailabilityStatus = detail.AvailabilityStatus
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTechnician(SuperAdminEditTechnicianViewModel model)
        {
            ViewData["Title"] = "Edit Technician";

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var (succeeded, errors) = await _platformAdmin.UpdateTechnicianAsync(model);
            if (!succeeded)
            {
                foreach (var error in errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                return View(model);
            }

            await LogActionAsync(
                "UpdatedTechnician",
                nameof(ApplicationUser),
                model.UserId,
                $"Updated technician profile for {model.FullName}.");

            TempData["SuccessMessage"] = "Technician details updated.";
            return RedirectToAction(nameof(TechnicianDetails), new { userId = model.UserId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTechnician(string userId)
        {
            var (success, error) = await _platformAdmin.HardDeleteTechnicianAsync(userId);
            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Could not delete technician.";
                return RedirectToAction(nameof(TechnicianDetails), new { userId });
            }

            await LogActionAsync(
                "DeletedTechnician",
                nameof(ApplicationUser),
                userId,
                $"Hard-deleted technician {userId}.");

            TempData["SuccessMessage"] = "Technician permanently deleted.";
            return RedirectToAction(nameof(Technicians));
        }

        public async Task<IActionResult> AuditLogs(string? user, string? actionFilter)
        {
            ViewData["Title"] = "Platform Audit Log";
            ViewBag.User = user;
            ViewBag.ActionFilter = actionFilter;
            return View(await _platformAdmin.GetPlatformAuditLogsAsync(user, actionFilter));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveTechnician(string userId)
        {
            if (!await _platformAdmin.ApproveTechnicianAsync(userId))
            {
                return NotFound();
            }

            await LogActionAsync("ApprovedTechnician", nameof(ApplicationUser), userId, $"Approved technician {userId}.");

            var user = await _userManager.FindByIdAsync(userId);
            if (user?.Email is not null)
            {
                await TrySendTechnicianEmailAsync(
                    user.Email,
                    TechnicianNotificationEmailBuilder.ApprovedSubject,
                    TechnicianNotificationEmailBuilder.BuildApprovedHtml(
                        user.FullName,
                        GetLoginUrl()));
            }

            TempData["SuccessMessage"] = "Technician approved. Notification email sent.";
            return RedirectToAction(nameof(Technicians), new { pendingOnly = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectTechnician(string userId, bool returnPending = true, string? reviewStatus = null)
        {
            if (!await _platformAdmin.RejectTechnicianAsync(userId))
            {
                return NotFound();
            }

            await LogActionAsync("RejectedTechnician", nameof(ApplicationUser), userId, $"Rejected technician {userId}.");

            var user = await _userManager.FindByIdAsync(userId);
            if (user?.Email is not null)
            {
                await TrySendTechnicianEmailAsync(
                    user.Email,
                    TechnicianNotificationEmailBuilder.RejectedSubject,
                    TechnicianNotificationEmailBuilder.BuildRejectedHtml(user.FullName));
            }

            TempData["SuccessMessage"] = returnPending
                ? "Technician rejected. Notification email sent."
                : "Technician deactivated. Notification email sent.";
            return returnPending
                ? RedirectToAction(nameof(Technicians), new { pendingOnly = true })
                : RedirectToAction(nameof(Technicians), new { pendingOnly = false, reviewStatus });
        }

        public async Task<IActionResult> PendingPayouts()
        {
            ViewData["Title"] = "Pending Bank Transfers";
            return View(await _platformAdmin.GetPendingPayoutsAsync());
        }

        [HttpGet]
        public async Task<IActionResult> CommissionReport(DateTime? from = null, DateTime? to = null, int? companyId = null)
        {
            var query = _context.TechnicianPayments
                .Include(p => p.MaintenanceRequest)
                .Include(p => p.Manager)
                .Include(p => p.Technician)
                    .ThenInclude(t => t.User)
                .Include(p => p.Company)
                .Where(p => p.PaymentStatus == TechnicianPaymentStatuses.Paid);

            if (from.HasValue)
            {
                var fromUtc = DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc);
                query = query.Where(p => p.PaidAt >= fromUtc);
            }

            if (to.HasValue)
            {
                var toUtcExclusive = DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Utc);
                query = query.Where(p => p.PaidAt < toUtcExclusive);
            }

            if (companyId.HasValue)
            {
                query = query.Where(p => p.CompanyId == companyId.Value);
            }

            var payments = await query
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

            var companies = await _context.PropertyManagementCompanies
                .OrderBy(c => c.CompanyName)
                .Select(c => new CompanyFilterOptionViewModel(c.CompanyId, c.CompanyName))
                .ToListAsync();

            return View("~/Areas/SuperAdmin/Views/SuperAdmin/CommissionReport.cshtml", new TechnicianPaymentReportViewModel
            {
                Payments = payments,
                TotalGrossAmount = payments.Sum(p => p.GrossAmount),
                TotalPlatformCommission = payments.Sum(p => p.PlatformCommissionAmount),
                TotalTechnicianNetAmount = payments.Sum(p => p.TechnicianNetAmount),
                PaymentCount = payments.Count,
                From = from,
                To = to,
                CompanyId = companyId,
                Companies = companies
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPayoutTransferred(MarkPayoutTransferredViewModel input)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var (success, error) = await _payoutService.MarkTransferredAsync(
                input.PaymentId,
                user.Id,
                input.BankTransferReference);

            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Could not mark payout as transferred.";
            }
            else
            {
                await LogActionAsync(
                    "MarkedPayoutTransferred",
                    nameof(TechnicianPayout),
                    input.PaymentId.ToString(),
                    $"Marked bank transfer sent for payment {input.PaymentId}.");
                TempData["SuccessMessage"] = "Bank transfer marked as sent.";
            }

            return RedirectToAction(nameof(PendingPayouts));
        }

        private void ValidateTechnicianCertificates(SuperAdminCreateTechnicianViewModel model)
        {
            const long maxCertificateFileSize = 10 * 1024 * 1024;
            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png"
            };
            var allowedMimeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "application/pdf",
                "image/jpeg",
                "image/png",
                "application/msword",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            };

            foreach (var file in model.Certificates.Where(f => f.Length > 0))
            {
                if (file.Length > maxCertificateFileSize)
                {
                    ModelState.AddModelError(nameof(model.Certificates), "Each certificate or document must be 10MB or smaller.");
                }

                var extension = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError(nameof(model.Certificates), "Upload PDF, DOC, DOCX, JPG, JPEG, or PNG files only.");
                }

                if (!string.IsNullOrWhiteSpace(file.ContentType) &&
                    !allowedMimeTypes.Contains(file.ContentType.Trim()))
                {
                    ModelState.AddModelError(nameof(model.Certificates), "Upload PDF, DOC, DOCX, JPG, JPEG, or PNG files only.");
                }
            }
        }

        private async Task<AreaDashboardViewModel?> BuildProfileModelAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return null;
            }

            var displayName = string.IsNullOrWhiteSpace(user.FullName) ? "Super Admin" : user.FullName.Trim();
            var initials = string.Concat(
                displayName
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Take(2)
                    .Select(part => part[0]))
                .ToUpperInvariant();

            return new AreaDashboardViewModel
            {
                CurrentUserName = displayName,
                CurrentUserEmail = user.Email ?? string.Empty,
                CurrentUserPhoneNumber = user.PhoneNumber,
                CurrentUserInitials = string.IsNullOrWhiteSpace(initials) ? "SA" : initials,
                CurrentUserAvatarUrl = user.AvatarUrl
            };
        }

        private string GetLoginUrl()
        {
            var baseUrl = string.IsNullOrWhiteSpace(_appSettings.BaseUrl)
                ? $"{Request.Scheme}://{Request.Host}"
                : _appSettings.BaseUrl.TrimEnd('/');

            return $"{baseUrl}/Identity/Account/Login";
        }

        private async Task TrySendTechnicianEmailAsync(string to, string subject, string htmlBody)
        {
            try
            {
                await _emailService.SendEmailAsync(to, subject, htmlBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send technician email to {Recipient} with subject {Subject}.", to, subject);
            }
        }

        private async Task LogActionAsync(string action, string entityName, string entityId, string description)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                await _platformAdmin.AddAuditLogAsync(user.Id, action, entityName, entityId, description);
            }
        }
    }
}
