using System.ComponentModel.DataAnnotations;

namespace CloudMVCApplication.ViewModels
{
    public class ManagerAssignmentViewModel
    {
        [Required]
        public int RequestId { get; set; }

        [Required]
        public int TechnicianId { get; set; }

        [Required]
        public string Priority { get; set; } = "Medium";

        [StringLength(500)]
        public string? Notes { get; set; }
    }

    public class ManagerRequestUpdateViewModel
    {
        [Required]
        public int RequestId { get; set; }

        [Required]
        public string Status { get; set; } = "Pending";

        [Required]
        public string Priority { get; set; } = "Medium";

        [StringLength(500)]
        public string? Notes { get; set; }
    }

    public class ManagerProfileUpdateViewModel
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

    public class ManagerPaymentReleaseViewModel
    {
        public int RequestId { get; set; }
        public int? PaymentId { get; set; }
        public decimal? Amount { get; set; }
        public decimal DefaultAmount { get; set; }
        public decimal PlatformFeeAmount { get; set; }
        public decimal TechnicianNetAmount { get; set; }
        public decimal PlatformFeePercent { get; set; }
        public string? PaymentStatus { get; set; }
        public string? TechnicianPayoutStatus { get; set; }
        public DateTime? BankTransferredAt { get; set; }
        public string? FailureReason { get; set; }
        public DateTime? PaidAt { get; set; }
        public bool CompanyHasBilling { get; set; }
        public bool TechnicianPayoutReady { get; set; }
        public bool CanRelease { get; set; }
        public string? BlockReason { get; set; }
        public string RequestTitle { get; set; } = string.Empty;
        public string? RequestDescription { get; set; }
        public string TechnicianName { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public string UnitNumber { get; set; } = string.Empty;
        public string Currency { get; set; } = "MYR";
        public string PaymentMethod { get; set; } = "FPX";
        public string? Notes { get; set; }
        public string? TransactionReference { get; set; }
        public string? CheckoutUrl { get; set; }
    }

    public class ManagerReleasePaymentViewModel
    {
        [Required]
        public int RequestId { get; set; }

        [Range(0.01, 999999)]
        public decimal Amount { get; set; }

        [StringLength(80)]
        public string PaymentMethod { get; set; } = "FPX";

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
