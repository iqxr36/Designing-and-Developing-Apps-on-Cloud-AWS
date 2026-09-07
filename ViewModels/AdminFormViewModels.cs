using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace CloudMVCApplication.ViewModels
{
    public class AdminUserCreateViewModel
    {
        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Phone]
        public string? PhoneNumber { get; set; }

        [Required]
        public string Role { get; set; } = string.Empty;

        [Required]
        public string Status { get; set; } = "Active";

        [Required]
        [MinLength(8)]
        public string TemporaryPassword { get; set; } = string.Empty;
    }

    public class AdminUserEditViewModel
    {
        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Phone]
        public string? PhoneNumber { get; set; }

        [Required]
        public string Role { get; set; } = string.Empty;
    }

    public class AdminPropertyViewModel
    {
        [Required]
        public string PropertyName { get; set; } = string.Empty;

        [Required]
        public string PropertyType { get; set; } = "Residential Building";

        public string? ImageUrl { get; set; }

        public IFormFile? PropertyPhoto { get; set; }

        public string? Address { get; set; }

        public string? City { get; set; }

        [Required]
        public string Status { get; set; } = "Active";

        [Range(0, 500, ErrorMessage = "Initial unit count must be between 0 and 500.")]
        public int InitialUnitCount { get; set; }
    }

    public class AdminUnitViewModel
    {
        [Required]
        public string UnitNumber { get; set; } = string.Empty;

        [Required]
        public int PropertyId { get; set; }

        [Required]
        public string UnitType { get; set; } = string.Empty;

        public int? FloorNumber { get; set; }

        [Required]
        public string Status { get; set; } = "Vacant";

        public string? TenantUserId { get; set; }
    }

    public class AdminMaintenanceRequestViewModel
    {
        [Required]
        public int UnitId { get; set; }

        [Required]
        public string Category { get; set; } = string.Empty;

        [Required]
        public string Priority { get; set; } = "Medium";

        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public int? TechnicianId { get; set; }
    }

    public class AdminRequestStatusViewModel
    {
        [Required]
        public string Status { get; set; } = "Pending";

        public int? TechnicianId { get; set; }

        public string? Notes { get; set; }
    }

    public class AdminProfileUpdateViewModel
    {
        [Required]
        public string FullName { get; set; } = string.Empty;

        [Phone]
        public string? PhoneNumber { get; set; }

        [DataType(DataType.Password)]
        public string? CurrentPassword { get; set; }

        [DataType(DataType.Password)]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "The new password and confirmation password do not match.")]
        public string? ConfirmPassword { get; set; }
    }

    public class AdminCompanySettingsViewModel
    {
        [Required]
        public string CompanyName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string CompanyEmail { get; set; } = string.Empty;

        [Phone]
        public string? CompanyPhone { get; set; }

        public string? CompanyAddress { get; set; }
    }

    public class AdminNotificationSettingsViewModel
    {
        public bool UrgentMaintenanceAlerts { get; set; }

        public bool WeeklyReportSummaries { get; set; }
    }

    public class AdminBrandingSettingsViewModel
    {
        [Required]
        [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Enter a six-digit hex colour such as #570000.")]
        public string PrimaryColor { get; set; } = "#570000";

        [Required]
        public string SidebarTheme { get; set; } = "Enterprise Dark";
    }
}
