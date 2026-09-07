using CloudMVCApplication.Models;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace CloudMVCApplication.ViewModels
{
    public class TechnicianJobDetailViewModel
    {
        public MaintenanceRequest Request { get; set; } = null!;
        public IReadOnlyList<string> StatusOptions { get; set; } = Array.Empty<string>();
    }

    public class TechnicianStatusUpdateViewModel
    {
        public int RequestId { get; set; }
        public string DisplayId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string CurrentStatus { get; set; } = string.Empty;
        public IReadOnlyList<string> StatusOptions { get; set; } = Array.Empty<string>();

        [Required]
        public string NewStatus { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Notes { get; set; }
    }

    public class TechnicianProofUploadViewModel
    {
        public IReadOnlyList<RequestRowViewModel> ActiveJobs { get; set; } = Array.Empty<RequestRowViewModel>();
        public RequestRowViewModel? SelectedJob { get; set; }
        public int? RequestId { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public IReadOnlyList<IFormFile> Files { get; set; } = Array.Empty<IFormFile>();
    }

    public class TechnicianSettingsUpdateViewModel
    {
        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Phone]
        public string? PhoneNumber { get; set; }

        [StringLength(80)]
        public string Specialization { get; set; } = "General Maintenance";

        [StringLength(40)]
        public string AvailabilityStatus { get; set; } = "Available";
    }

    public class TechnicianPasswordUpdateViewModel
    {
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 8)]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class TechnicianEarningsViewModel
    {
        public decimal TotalPaidToBank { get; set; }
        public decimal AwaitingTransferAmount { get; set; }
        public decimal PendingReleaseAmount { get; set; }
        public int PaidToBankJobs { get; set; }
        public DateTime? LatestBankPayoutAt { get; set; }
        public IReadOnlyList<TechnicianPaymentRowViewModel> Payments { get; set; } = Array.Empty<TechnicianPaymentRowViewModel>();
    }

    public record TechnicianPaymentRowViewModel(
        int PaymentId,
        int RequestId,
        string DisplayId,
        string RequestTitle,
        string PropertyName,
        string UnitNumber,
        decimal GrossAmount,
        decimal PlatformFeeAmount,
        decimal TechnicianNetAmount,
        string PaymentStatus,
        string? TechnicianPayoutStatus,
        string DisplayStatus,
        DateTime CreatedAt,
        DateTime? PaidAt,
        DateTime? BankTransferredAt,
        string? TransactionReference);

    public class TechnicianPayoutStatusViewModel
    {
        public bool IsReady { get; set; }
        public bool CanSaveDetails { get; set; }
        public string? AccountHolderName { get; set; }
        public string? BankName { get; set; }
        public string? AccountNumberMasked { get; set; }
        public DateTime? SetupCompletedAt { get; set; }
        public string? BlockReason { get; set; }
    }

    public class TechnicianPayoutDetailsViewModel
    {
        [Required]
        [StringLength(120)]
        public string AccountHolderName { get; set; } = string.Empty;

        [Required]
        [StringLength(80)]
        public string BankName { get; set; } = string.Empty;

        [Required]
        [StringLength(40)]
        public string AccountNumber { get; set; } = string.Empty;
    }
}
