using System.ComponentModel.DataAnnotations;
using CloudMVCApplication.Data;
using CloudMVCApplication.Models;
using CloudMVCApplication.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CloudMVCApplication.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private const string PendingPaymentLoginRedirectAction = "PendingPaymentLoginRedirect";
        private const string SuspiciousPendingPaymentLoginAction = "SuspiciousPendingPaymentLogin";

        private readonly ApplicationDbContext _context;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly XenditSubscriptionService _xenditSubscription;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            ApplicationDbContext context,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            XenditSubscriptionService xenditSubscription,
            ILogger<LoginModel> logger)
        {
            _context = context;
            _signInManager = signInManager;
            _userManager = userManager;
            _xenditSubscription = xenditSubscription;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string ReturnUrl { get; set; } = string.Empty;

        public bool PasswordResetSucceeded { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Display(Name = "Remember me")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string? returnUrl = null, bool passwordReset = false)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            PasswordResetSucceeded = passwordReset;
            ReturnUrl = returnUrl ?? Url.Content("~/");

            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user != null && await _userManager.IsInRoleAsync(user, "SuperAdmin"))
            {
                ModelState.AddModelError(string.Empty, "Use the platform admin login at /SuperAdmin/Login.");
                return Page();
            }

            if (user != null && !user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Your account is inactive.");
                return Page();
            }

            var result = user == null
                ? Microsoft.AspNetCore.Identity.SignInResult.Failed
                : await _signInManager.CheckPasswordSignInAsync(user, Input.Password, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }

                var authenticatedUser = user;

                if (user?.CompanyId != null)
                {
                    var company = await _context.PropertyManagementCompanies
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.CompanyId == authenticatedUser.CompanyId!.Value);

                    if (company != null && company.SubscriptionStatus == CompanySubscriptionStatuses.PendingPayment)
                    {
                        var checkoutUrl = await _xenditSubscription.GetOrCreatePendingCheckoutUrlAsync(company.CompanyId);
                        if (!string.IsNullOrWhiteSpace(checkoutUrl))
                        {
                            await TrackPendingPaymentLoginAsync(authenticatedUser, company);
                            return Redirect(checkoutUrl);
                        }

                        ModelState.AddModelError(string.Empty, "Your subscription payment is pending and we could not open checkout. Please contact support.");
                        return Page();
                    }
                }

                await _signInManager.SignInAsync(authenticatedUser, Input.RememberMe);
                _logger.LogInformation("User logged in.");

                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl != Url.Content("~/"))
                {
                    return LocalRedirect(returnUrl);
                }

                if (user != null)
                {
                    return LocalRedirect(await GetDashboardUrlAsync(user));
                }

                return LocalRedirect(ReturnUrl);
            }

            if (result.RequiresTwoFactor)
            {
                return RedirectToPage("./LoginWith2fa", new { ReturnUrl, Input.RememberMe });
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out.");
                return RedirectToPage("./Lockout");
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return Page();
        }

        private async Task TrackPendingPaymentLoginAsync(ApplicationUser user, PropertyManagementCompany company)
        {
            var lookbackStart = DateTime.UtcNow.AddMinutes(-15);
            var recentRedirects = await _context.AuditLogs
                .AsNoTracking()
                .CountAsync(log =>
                    log.UserId == user.Id &&
                    log.Action == PendingPaymentLoginRedirectAction &&
                    log.EntityName == nameof(PropertyManagementCompany) &&
                    log.EntityId == company.CompanyId.ToString() &&
                    log.CreatedAt >= lookbackStart);

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = user.Id,
                Action = PendingPaymentLoginRedirectAction,
                EntityName = nameof(PropertyManagementCompany),
                EntityId = company.CompanyId.ToString(),
                Description = $"Redirected pending-payment user {user.Email} to checkout."
            });

            if (recentRedirects >= 2)
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = user.Id,
                    Action = SuspiciousPendingPaymentLoginAction,
                    EntityName = nameof(PropertyManagementCompany),
                    EntityId = company.CompanyId.ToString(),
                    Description = $"Repeated pending-payment login attempts detected for {user.Email}. Attempts in the last 15 minutes: {recentRedirects + 1}."
                });
            }

            await _context.SaveChangesAsync();
        }

        private async Task<string> GetDashboardUrlAsync(ApplicationUser user)
        {
            if (await _userManager.IsInRoleAsync(user, "Administrator"))
            {
                return Url.Content("~/Administrator/Dashboard");
            }

            if (await _userManager.IsInRoleAsync(user, "Manager"))
            {
                return Url.Content("~/Manager/Dashboard");
            }

            if (await _userManager.IsInRoleAsync(user, "Tenant"))
            {
                return Url.Content("~/Tenant/Dashboard");
            }

            if (await _userManager.IsInRoleAsync(user, "Technician"))
            {
                return Url.Content("~/Technician/Dashboard");
            }

            return Url.Content("~/");
        }
    }
}
