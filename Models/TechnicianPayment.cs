using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{
    public class TechnicianPayment
    {
        public const decimal CommissionRate = 10m;

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int MaintenanceRequestId { get; set; }

        [Required]
        public string ManagerId { get; set; } = string.Empty;

        [Required]
        public int TechnicianId { get; set; }

        [Required]
        public int CompanyId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossAmount { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal PlatformCommissionRate { get; set; } = CommissionRate;

        [Column(TypeName = "decimal(18,2)")]
        public decimal PlatformCommissionAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TechnicianNetAmount { get; set; }

        [StringLength(3)]
        public string Currency { get; set; } = "MYR";

        [StringLength(80)]
        public string PaymentMethod { get; set; } = "FPX";

        [StringLength(40)]
        public string PaymentProvider { get; set; } = "Xendit";

        [StringLength(120)]
        public string? ProviderInvoiceId { get; set; }

        [StringLength(120)]
        public string? ProviderPaymentReference { get; set; }

        [StringLength(500)]
        public string? CheckoutUrl { get; set; }

        [StringLength(20)]
        public string PaymentStatus { get; set; } = TechnicianPaymentStatuses.Pending;

        [StringLength(80)]
        public string TransactionReference { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime? PaidAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public string? RawWebhookPayload { get; set; }

        public TechnicianPayout? Payout { get; set; }

        [ForeignKey(nameof(MaintenanceRequestId))]
        public MaintenanceRequest MaintenanceRequest { get; set; } = null!;

        [ForeignKey(nameof(ManagerId))]
        public ApplicationUser Manager { get; set; } = null!;

        [ForeignKey(nameof(TechnicianId))]
        public TechnicianProfile Technician { get; set; } = null!;

        [ForeignKey(nameof(CompanyId))]
        public PropertyManagementCompany Company { get; set; } = null!;
    }
}
