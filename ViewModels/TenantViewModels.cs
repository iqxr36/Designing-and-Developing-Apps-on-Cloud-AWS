using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace CloudMVCApplication.ViewModels
{
    public class TenantMaintenanceRequestViewModel
    {
        [Required]
        [StringLength(120)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public string Category { get; set; } = string.Empty;

        public List<IFormFile> Files { get; set; } = new();
    }

    public class TenantIssuePhotoUploadViewModel
    {
        [Required]
        public int RequestId { get; set; }

        public List<IFormFile> Files { get; set; } = new();
    }

    public class TenantFeedbackViewModel
    {
        [Required]
        public int RequestId { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; } = 5;

        [StringLength(1000)]
        public string? Comment { get; set; }
    }

    public class TenantProfileUpdateViewModel
    {
        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Phone]
        public string? PhoneNumber { get; set; }

        public string? CurrentPassword { get; set; }

        [StringLength(100, MinimumLength = 8)]
        public string? NewPassword { get; set; }

        [Compare(nameof(NewPassword), ErrorMessage = "The new password and confirmation password do not match.")]
        public string? ConfirmPassword { get; set; }
    }
}
