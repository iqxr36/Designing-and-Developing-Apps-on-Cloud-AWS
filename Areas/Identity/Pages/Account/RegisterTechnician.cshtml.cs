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
    public class RegisterTechnicianModel : PageModel
    {
        private const string TechnicianRoleName = "Technician";
        private const long MaxCertificateFileSize = 10 * 1024 * 1024;
        private static readonly HashSet<string> AllowedCertificateExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf",
            ".jpg",
            ".jpeg",
            ".png",
            ".doc",
            ".docx"
        };
        private static readonly HashSet<string> AllowedCertificateMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "image/jpeg",
            "image/png",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };

        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly IFileStorageService _fileStorage;
        private readonly ILogger<RegisterTechnicianModel> _logger;

        public RegisterTechnicianModel(
            ApplicationDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            IFileStorageService fileStorage,
            ILogger<RegisterTechnicianModel> logger)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _roleManager = roleManager;
            _fileStorage = fileStorage;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public bool RegistrationSubmitted { get; set; }

        /// <summary>
        /// When set, the registration page shows a validation-error popup.
        /// </summary>
        public string? DocumentErrorPopupMessage { get; set; }

        public void OnGet(bool registered = false)
        {
            RegistrationSubmitted = registered;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            ValidateCertificates();

            if (!ModelState.IsValid)
            {
                DocumentErrorPopupMessage = GetValidationPopupMessage();
                return Page();
            }

            var user = new ApplicationUser
            {
                UserName = Input.Email,
                Email = Input.Email,
                PhoneNumber = Input.PhoneNumber,
                FullName = Input.FullName,
                CompanyId = null,
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            };

            var createUserResult = await _userManager.CreateAsync(user, Input.Password);
            if (!createUserResult.Succeeded)
            {
                AddIdentityErrors(createUserResult);
                return Page();
            }

            try
            {
                var roleCreationResult = await EnsureTechnicianRoleExistsAsync();
                if (!roleCreationResult.Succeeded)
                {
                    await RollbackCreatedUserAsync(user);
                    AddIdentityErrors(roleCreationResult);
                    return Page();
                }

                var roleResult = await _userManager.AddToRoleAsync(user, TechnicianRoleName);
                if (!roleResult.Succeeded)
                {
                    await RollbackCreatedUserAsync(user);
                    AddIdentityErrors(roleResult);
                    return Page();
                }

                var technicianProfile = new TechnicianProfile
                {
                    UserId = user.Id,
                    Specialization = Input.Specialization,
                    ExperienceYears = Input.ExperienceYears,
                    ServiceArea = Input.ServiceArea,
                    AvailabilityStatus = "Pending Review",
                    AverageRating = 0,
                    TotalCompletedJobs = 0,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.TechnicianProfiles.Add(technicianProfile);
                await _dbContext.SaveChangesAsync();
                await SaveCertificateDocumentsAsync(technicianProfile, user.Id);

                _logger.LogInformation("Technician registration submitted for {Email}.", Input.Email);
                return RedirectToPage(new { registered = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Technician registration failed after user create for {Email}. Rolling back.", Input.Email);
                await RollbackCreatedUserAsync(user);
                const string message =
                    "Document upload failed. Your account was not registered—please try again with valid files.";
                ModelState.AddModelError("Input.Certificates", message);
                DocumentErrorPopupMessage = message;
                return Page();
            }
        }

        private async Task RollbackCreatedUserAsync(ApplicationUser user)
        {
            try
            {
                var profile = await _dbContext.TechnicianProfiles
                    .FirstOrDefaultAsync(p => p.UserId == user.Id);
                if (profile != null)
                {
                    var docs = _dbContext.TechnicianDocuments
                        .Where(d => d.TechnicianId == profile.TechnicianId);
                    _dbContext.TechnicianDocuments.RemoveRange(docs);
                    _dbContext.TechnicianProfiles.Remove(profile);
                    await _dbContext.SaveChangesAsync();
                }

                await _userManager.DeleteAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to roll back technician user {UserId} after registration error.", user.Id);
            }
        }

        private async Task<IdentityResult> EnsureTechnicianRoleExistsAsync()
        {
            if (await _roleManager.RoleExistsAsync(TechnicianRoleName))
            {
                return IdentityResult.Success;
            }

            return await _roleManager.CreateAsync(new ApplicationRole
            {
                Name = TechnicianRoleName,
                Description = "Technician users who can receive and complete maintenance assignments."
            });
        }

        private void AddIdentityErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        private void ValidateCertificates()
        {
            var files = Input.Certificates
                .Where(f => f != null && f.Length > 0)
                .ToList();

            if (files.Count == 0)
            {
                ModelState.AddModelError(
                    "Input.Certificates",
                    "Please upload at least one certificate or document (PDF, DOC, DOCX, JPG, JPEG, or PNG).");
                return;
            }

            foreach (var file in files)
            {
                if (file.Length > MaxCertificateFileSize)
                {
                    ModelState.AddModelError(
                        "Input.Certificates",
                        "Each certificate or document must be 10MB or smaller.");
                }

                var extension = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(extension) || !AllowedCertificateExtensions.Contains(extension))
                {
                    ModelState.AddModelError(
                        "Input.Certificates",
                        "Upload PDF, DOC, DOCX, JPG, JPEG, or PNG files only.");
                }

                if (!string.IsNullOrWhiteSpace(file.ContentType) &&
                    !AllowedCertificateMimeTypes.Contains(file.ContentType.Trim()))
                {
                    ModelState.AddModelError(
                        "Input.Certificates",
                        "Upload PDF, DOC, DOCX, JPG, JPEG, or PNG files only.");
                }
            }

            if (Input.Certificates.Any(f => f != null && f.Length == 0 && !string.IsNullOrWhiteSpace(f.FileName)))
            {
                ModelState.AddModelError("Input.Certificates", "Uploaded documents cannot be empty.");
            }
        }

        private string? GetValidationPopupMessage()
        {
            var certErrors = ModelState["Input.Certificates"]?.Errors
                .Select(e => e.ErrorMessage)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct()
                .ToList();

            if (certErrors is { Count: > 0 })
            {
                return string.Join(" ", certErrors);
            }

            var otherErrors = ModelState
                .Where(kvp => kvp.Key != "Input.Certificates" && kvp.Value?.Errors.Count > 0)
                .SelectMany(kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage))
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct()
                .ToList();

            if (otherErrors.Count == 0)
            {
                return null;
            }

            return "Please complete all required fields correctly. " + string.Join(" ", otherErrors);
        }

        private async Task SaveCertificateDocumentsAsync(TechnicianProfile technicianProfile, string userId)
        {
            foreach (var file in Input.Certificates.Where(f => f != null && f.Length > 0))
            {
                var originalFileName = Path.GetFileName(file.FileName);
                var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
                var storedFileName = $"{Guid.NewGuid():N}{extension}";

                var filePath = await _fileStorage.UploadAsync(
                    file,
                    $"technician-documents/{userId}",
                    storedFileName,
                    HttpContext.RequestAborted);

                _dbContext.TechnicianDocuments.Add(new TechnicianDocument
                {
                    TechnicianId = technicianProfile.TechnicianId,
                    FileName = originalFileName,
                    FilePath = filePath,
                    FileType = string.IsNullOrWhiteSpace(file.ContentType) ? extension.TrimStart('.') : file.ContentType,
                    UploadedAt = DateTime.UtcNow
                });
            }

            await _dbContext.SaveChangesAsync();
        }

        public class InputModel
        {
            [Required]
            [Display(Name = "Full Name")]
            public string FullName { get; set; } = string.Empty;

            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            [Phone]
            [Display(Name = "Phone Number")]
            public string PhoneNumber { get; set; } = string.Empty;

            [Required]
            public string Specialization { get; set; } = string.Empty;

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Display(Name = "Confirm Password")]
            [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; } = string.Empty;

            [Required]
            [Range(0, 80)]
            [Display(Name = "Experience (Years)")]
            public int ExperienceYears { get; set; }

            [Required]
            [Display(Name = "Service Area")]
            public string ServiceArea { get; set; } = string.Empty;

            [Display(Name = "Certificates & Documents")]
            public List<IFormFile> Certificates { get; set; } = new();
        }
    }
}
