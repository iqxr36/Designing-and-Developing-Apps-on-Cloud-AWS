using System.ComponentModel.DataAnnotations;
using System.Text;
using CloudMVCApplication.Models;
using CloudMVCApplication.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace CloudMVCApplication.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly AppSettings _appSettings;
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(
            UserManager<ApplicationUser> userManager,
            IEmailService emailService,
            IOptions<AppSettings> appSettings,
            ILogger<ForgotPasswordModel> logger)
        {
            _userManager = userManager;
            _emailService = emailService;
            _appSettings = appSettings.Value;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public bool RecoverySubmitted { get; set; }

        public void OnGet(bool submitted = false)
        {
            RecoverySubmitted = submitted;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user is not null)
            {
                try
                {
                    var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var resetUrl = BuildResetUrl(code, user.Email ?? Input.Email);
                    var htmlBody = PasswordResetEmailBuilder.BuildHtml(resetUrl);

                    await _emailService.SendEmailAsync(
                        user.Email ?? Input.Email,
                        PasswordResetEmailBuilder.Subject,
                        htmlBody);

                    _logger.LogInformation("Password reset email sent for {Email}.", Input.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send password reset email for {Email}.", Input.Email);
                }
            }

            return RedirectToPage(new { submitted = true });
        }

        private string BuildResetUrl(string code, string email)
        {
            var baseUrl = string.IsNullOrWhiteSpace(_appSettings.BaseUrl)
                ? $"{Request.Scheme}://{Request.Host}"
                : _appSettings.BaseUrl.TrimEnd('/');

            var resetPath = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", code, email });

            return $"{baseUrl}{resetPath}";
        }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;
        }
    }
}
