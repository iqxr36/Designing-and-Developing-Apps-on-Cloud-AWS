using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{
    public static class CompanySubscriptionEventTypes
    {
        public const string CheckoutCompleted = "CheckoutCompleted";
        public const string SubscriptionCreated = "SubscriptionCreated";
        public const string SubscriptionUpdated = "SubscriptionUpdated";
        public const string SubscriptionCancelled = "SubscriptionCancelled";
        public const string PaymentFailed = "PaymentFailed";
        public const string InvoicePaid = "InvoicePaid";
        public const string InvoiceFinalized = "InvoiceFinalized";
        public const string PlanChanged = "PlanChanged";
        public const string StatusChanged = "StatusChanged";
    }

    public class CompanySubscriptionEvent
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int EventId { get; set; }

        [Required]
        public int CompanyId { get; set; }

        [Required]
        public string EventType { get; set; } = string.Empty;

        public string? ProviderEventId { get; set; }

        public string? ProviderInvoiceId { get; set; }

        public string? PreviousPlan { get; set; }

        public string? NewPlan { get; set; }

        public string? PreviousStatus { get; set; }

        public string? NewStatus { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Amount { get; set; }

        public string? Currency { get; set; }

        public string? PayloadJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(CompanyId))]
        public PropertyManagementCompany Company { get; set; } = null!;
    }
}
