using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{
    public class CompanyInvoice
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int InvoiceId { get; set; }

        [Required]
        public int CompanyId { get; set; }

        [Required]
        public string ProviderInvoiceId { get; set; } = string.Empty;

        public string? ProviderSubscriptionId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountDue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; }

        [Required]
        public string Currency { get; set; } = "usd";

        [Required]
        public string Status { get; set; } = string.Empty;

        public DateTime? PeriodStart { get; set; }

        public DateTime? PeriodEnd { get; set; }

        public string? HostedInvoiceUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey(nameof(CompanyId))]
        public PropertyManagementCompany Company { get; set; } = null!;
    }
}
