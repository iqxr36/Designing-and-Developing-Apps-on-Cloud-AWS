using System.ComponentModel.DataAnnotations;
using CloudMVCApplication.Models;
using Microsoft.AspNetCore.Http;

namespace CloudMVCApplication.ViewModels
{
    public class SuperAdminDashboardViewModel
    {
        public int TotalCompanies { get; set; }
        public int ActiveCompanies { get; set; }
        public int SuspendedCompanies { get; set; }
        public int PendingTechnicians { get; set; }
        public int PendingBankTransfers { get; set; }
        public decimal TotalPlatformFeesEarned { get; set; }
        public decimal PendingPlatformFees { get; set; }
        public int PastDueSubscriptions { get; set; }
        public List<SuperAdminPlanCountViewModel> PlanBreakdown { get; set; } = [];
        public List<SuperAdminCompanyListItemViewModel> RecentCompanies { get; set; } = [];
    }

    public class SuperAdminPlanCountViewModel
    {
        public string Plan { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class SuperAdminCompanyListItemViewModel
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyEmail { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string SubscriptionPlan { get; set; } = string.Empty;
        public string SubscriptionStatus { get; set; } = string.Empty;
        public int PropertyCount { get; set; }
        public int UnitCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SuperAdminCompanyDetailViewModel : SuperAdminCompanyListItemViewModel
    {
        public string? CompanyPhone { get; set; }
        public string? CompanyAddress { get; set; }
        public string? ProviderCustomerId { get; set; }
        public string? ProviderSubscriptionId { get; set; }
        public int MaxProperties { get; set; }
        public int MaxUnits { get; set; }
        public int MaxManagers { get; set; }
        public int AdministratorCount { get; set; }
        public int ManagerCount { get; set; }
        public DateTime? CurrentPeriodStart { get; set; }
        public DateTime? CurrentPeriodEnd { get; set; }
        public DateTime? TrialEndsAt { get; set; }
        public List<SuperAdminCompanyAdminViewModel> Administrators { get; set; } = [];
        public List<SuperAdminBillingEventViewModel> BillingEvents { get; set; } = [];
        public List<SuperAdminInvoiceViewModel> Invoices { get; set; } = [];
    }

    public class SuperAdminBillingEventViewModel
    {
        public string EventType { get; set; } = string.Empty;
        public string? PreviousPlan { get; set; }
        public string? NewPlan { get; set; }
        public string? PreviousStatus { get; set; }
        public string? NewStatus { get; set; }
        public decimal? Amount { get; set; }
        public string? Currency { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SuperAdminInvoiceViewModel
    {
        public string ProviderInvoiceId { get; set; } = string.Empty;
        public decimal AmountDue { get; set; }
        public decimal AmountPaid { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? PeriodStart { get; set; }
        public DateTime? PeriodEnd { get; set; }
        public string? HostedInvoiceUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SuperAdminCompanyAdminViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int? CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SuperAdminTechnicianViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public bool IsActive { get; set; }
        public bool IsPending { get; set; }
        public string Specialization { get; set; } = string.Empty;
        public string AvailabilityStatus { get; set; } = string.Empty;
        public int ExperienceYears { get; set; }
        public string? ServiceArea { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? PayoutBankSummary { get; set; }
        public List<SuperAdminTechnicianDocumentViewModel> Documents { get; set; } = [];
        public bool HasDocuments => Documents.Count > 0;
    }

    public class SuperAdminTechnicianDocumentViewModel
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
    }

    public class SuperAdminLoginViewModel
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class SuperAdminPendingPayoutViewModel
    {
        public int PaymentId { get; set; }
        public int RequestId { get; set; }
        public string DisplayRequestId { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string TechnicianName { get; set; } = string.Empty;
        public string TechnicianEmail { get; set; } = string.Empty;
        public decimal CompanyChargeAmount { get; set; }
        public decimal PlatformFeeAmount { get; set; }
        public decimal TechnicianTransferAmount { get; set; }
        public DateTime? CompanyChargedAt { get; set; }
        public string AccountHolderName { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
    }

    public class MarkPayoutTransferredViewModel
    {
        [Required]
        public int PaymentId { get; set; }

        [StringLength(120)]
        public string? BankTransferReference { get; set; }
    }

    public class SuperAdminCreateCompanyViewModel
    {
        [Required]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Company Email")]
        public string CompanyEmail { get; set; } = string.Empty;

        [Phone]
        [Display(Name = "Company Phone")]
        public string? CompanyPhone { get; set; }

        [Display(Name = "Company Address")]
        public string? CompanyAddress { get; set; }

        [Required]
        [Display(Name = "Subscription Plan")]
        public string Plan { get; set; } = SubscriptionPlans.Professional;

        [Required]
        [Display(Name = "Administrator Name")]
        public string AdminName { get; set; } = string.Empty;

        [Required]
        [Phone]
        [Display(Name = "Administrator Phone")]
        public string AdminPhone { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class SuperAdminEditCompanyViewModel
    {
        public int CompanyId { get; set; }

        [Required]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Company Email")]
        public string CompanyEmail { get; set; } = string.Empty;

        [Phone]
        [Display(Name = "Company Phone")]
        public string? CompanyPhone { get; set; }

        [Display(Name = "Company Address")]
        public string? CompanyAddress { get; set; }
    }

    public class SuperAdminCreateTechnicianViewModel
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
        [StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        public string Specialization { get; set; } = string.Empty;

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

    public class SuperAdminEditTechnicianViewModel
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Phone]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Required]
        public string Specialization { get; set; } = string.Empty;

        [Required]
        [Range(0, 80)]
        [Display(Name = "Experience (Years)")]
        public int ExperienceYears { get; set; }

        [Required]
        [Display(Name = "Service Area")]
        public string ServiceArea { get; set; } = string.Empty;

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Availability Status")]
        public string AvailabilityStatus { get; set; } = "Available";
    }
}
