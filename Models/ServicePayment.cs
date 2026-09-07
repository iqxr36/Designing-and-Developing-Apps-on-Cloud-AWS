using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{
    public class ServicePayment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PaymentId { get; set; }

        [Required]
        public int RequestId { get; set; }

        public int? AssignmentId { get; set; }

        [Required]
        public string ManagerId { get; set; } = string.Empty;

        [Required]
        public int TechnicianId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public string PaymentStatus { get; set; } = ServicePaymentStatuses.Pending;

        public DateTime? PaidAt { get; set; }

        public string? ProviderPaymentIntentId { get; set; }

        public string? ProviderTransferId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PlatformFeeAmount { get; set; }

        public string? FailureReason { get; set; }

        public string? TechnicianPayoutStatus { get; set; }

        public DateTime? BankTransferredAt { get; set; }

        public string? BankTransferReference { get; set; }

        public string? BankTransferMarkedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(RequestId))]
        public MaintenanceRequest Request { get; set; } = null!;

        [ForeignKey(nameof(AssignmentId))]
        public Assignment? Assignment { get; set; }

        [ForeignKey(nameof(ManagerId))]
        public ApplicationUser Manager { get; set; } = null!;

        [ForeignKey(nameof(TechnicianId))]
        public TechnicianProfile Technician { get; set; } = null!;
    }
}
