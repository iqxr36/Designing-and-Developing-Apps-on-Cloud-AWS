using System.ComponentModel.DataAnnotations;
using CloudMVCApplication.Data;
using CloudMVCApplication.Models;
using CloudMVCApplication.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CloudMVCApplication.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterCompanyModel : PageModel
    {
        private const string PropertyAdminRoleName = "Administrator";

        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly XenditSubscriptionService _xenditSubscription;
        private readonly ILogger<RegisterCompanyModel> _logger;

        public RegisterCompanyModel(
            ApplicationDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            XenditSubscriptionService xenditSubscription,
            ILogger<RegisterCompanyModel> logger)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _roleManager = roleManager;
            _xenditSubscription = xenditSubscription;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public bool RegistrationSubmitted { get; set; }

        public string? BillingMessage { get; set; }

        public bool XenditCheckoutAvailable { get; private set; }

        public void OnGet(bool registered = false, string? billing = null)
        {
            RegistrationSubmitted = registered;
            XenditCheckoutAvailable = _xenditSubscription.IsConfigured;
            Input.Plan = string.IsNullOrWhiteSpace(Input.Plan) ? SubscriptionPlans.Professional : Input.Plan;
            BillingMessage = billing switch
            {
                "success" => "Payment received. You can now log in with your administrator account.",
                "cancel" => "Registration saved. Complete billing setup from your administrator account when ready.",
                "checkout_failed" => "Your company account was created, but we could not open the payment page. Try again from login or contact support.",
                "trial" => "Your company account was created with a 14-day trial. Log in with your administrator email and password.",
                _ => registered ? "Your company account was created. Log in with your administrator email and password." : null
            };
        }

        public async Task<IActionResult> OnPostAsync()
        {
            XenditCheckoutAvailable = _xenditSubscription.IsConfigured;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (!SubscriptionPlans.All.Contains(Input.Plan))
            {
                ModelState.AddModelError(nameof(Input.Plan), "Choose a valid subscription plan.");
                return Page();
            }

            var companyEmailExists = await _dbContext.PropertyManagementCompanies
                .AnyAsync(c => c.CompanyEmail == Input.CompanyEmail);

            if (companyEmailExists)
            {
                ModelState.AddModelError(nameof(Input.CompanyEmail), "A company with this business email already exists.");
                return Page();
            }

            var existingAdmin = await _userManager.FindByEmailAsync(Input.CompanyEmail);
            if (existingAdmin != null)
            {
                ModelState.AddModelError(nameof(Input.CompanyEmail), "An account with this email already exists.");
                return Page();
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            PropertyManagementCompany company;

            try
            {
            var (maxProperties, maxUnits, maxManagers) = SubscriptionPlans.GetLimits(Input.Plan);
            company = new PropertyManagementCompany
            {
                CompanyName = Input.CompanyName,
                CompanyEmail = Input.CompanyEmail,
                CompanyPhone = Input.CompanyPhone,
                CompanyAddress = Input.CompanyAddress,
                Status = "Active",
                SubscriptionPlan = Input.Plan,
                MaxProperties = maxProperties,
                MaxUnits = maxUnits,
                MaxManagers = maxManagers,
                SubscriptionStatus = _xenditSubscription.IsConfigured
                    ? CompanySubscriptionStatuses.PendingPayment
                    : CompanySubscriptionStatuses.Trialing,
                TrialEndsAt = DateTime.UtcNow.AddDays(14),
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.PropertyManagementCompanies.Add(company);
            await _dbContext.SaveChangesAsync();

            var adminUser = new ApplicationUser
            {
                UserName = Input.CompanyEmail,
                Email = Input.CompanyEmail,
                PhoneNumber = Input.AdminPhone,
                FullName = Input.AdminName,
                CompanyId = company.CompanyId,
                IsActive = true,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var createUserResult = await _userManager.CreateAsync(adminUser, Input.Password);
            if (!createUserResult.Succeeded)
            {
                AddIdentityErrors(createUserResult);
                await transaction.RollbackAsync();
                return Page();
            }

            var roleCreationResult = await EnsurePropertyAdminRoleExistsAsync();
            if (!roleCreationResult.Succeeded)
            {
                AddIdentityErrors(roleCreationResult);
                await transaction.RollbackAsync();
                return Page();
            }

            var roleResult = await _userManager.AddToRoleAsync(adminUser, PropertyAdminRoleName);
            if (!roleResult.Succeeded)
            {
                AddIdentityErrors(roleResult);
                await transaction.RollbackAsync();
                return Page();
            }

            _dbContext.AdministratorProfiles.Add(new AdministratorProfile
            {
                UserId = adminUser.Id,
                Department = "Operations",
                AccessLevel = "Company Administrator",
                CreatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            _logger.LogInformation(
                "Company registration submitted for {CompanyEmail} with plan {Plan}.",
                Input.CompanyEmail,
                Input.Plan);

            try
            {
                var checkoutUrl = await _xenditSubscription.CreateCheckoutAsync(
                    company.CompanyId,
                    Input.Plan);

                if (!string.IsNullOrWhiteSpace(checkoutUrl))
                {
                    return Redirect(checkoutUrl);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Xendit checkout failed for company {CompanyId}.", company.CompanyId);
                return RedirectToPage(new { registered = true, billing = "checkout_failed" });
            }

            var billingResult = _xenditSubscription.IsConfigured ? "checkout_failed" : "trial";
            return RedirectToPage(new { registered = true, billing = billingResult });
        }

        private async Task<IdentityResult> EnsurePropertyAdminRoleExistsAsync()
        {
            if (await _roleManager.RoleExistsAsync(PropertyAdminRoleName))
            {
                return IdentityResult.Success;
            }

            return await _roleManager.CreateAsync(new ApplicationRole
            {
                Name = PropertyAdminRoleName,
                Description = "Company administrator users who manage property operations."
            });
        }

        private void AddIdentityErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        public class InputModel
        {
            [Required]
            [Display(Name = "Company Name")]
            public string CompanyName { get; set; } = string.Empty;

            [Required]
            [EmailAddress]
            [Display(Name = "Company Email")]
            public string CompanyEmail { get; set; } = string.Empty;

            [Required(ErrorMessage = "Phone number is required.")]
            [Display(Name = "Phone Number")]
            public string CompanyPhone { get; set; } = string.Empty;

            [Required]
            [Display(Name = "Office Address")]
            public string CompanyAddress { get; set; } = string.Empty;

            [Required]
            [Display(Name = "Admin Name")]
            public string AdminName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Admin phone is required.")]
            [Display(Name = "Phone")]
            public string AdminPhone { get; set; } = string.Empty;

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 8)]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Required(ErrorMessage = "Please confirm your password.")]
            [DataType(DataType.Password)]
            [Display(Name = "Confirm Password")]
            [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; } = string.Empty;

            [Required]
            public string Plan { get; set; } = SubscriptionPlans.Professional;
        }
    }
}
