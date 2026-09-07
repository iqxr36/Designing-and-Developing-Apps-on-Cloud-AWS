using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{
    public class SubscriptionPayment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int CompanySubscriptionId { get; set; }

        [Required]
        public int CompanyId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossAmount { get; set; }

        [StringLength(3)]
        public string Currency { get; set; } = "MYR";

        [StringLength(40)]
        public string PaymentProvider { get; set; } = "Xendit";

        [StringLength(40)]
        public string PaymentMethod { get; set; } = "FPX";

        [StringLength(120)]
        public string? ProviderInvoiceId { get; set; }

        [StringLength(120)]
        public string? ProviderPaymentReference { get; set; }

        [StringLength(500)]
        public string? CheckoutUrl { get; set; }

        [StringLength(20)]
        public string PaymentStatus { get; set; } = PaymentStatuses.Pending;

        [StringLength(80)]
        public string TransactionReference { get; set; } = string.Empty;

        public DateTime? PaidAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public string? RawWebhookPayload { get; set; }

        [ForeignKey(nameof(CompanyId))]
        public PropertyManagementCompany Company { get; set; } = null!;

        [ForeignKey(nameof(CompanySubscriptionId))]
        public CompanySubscription CompanySubscription { get; set; } = null!;
    }
}
