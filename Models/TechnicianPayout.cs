using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{
    public class TechnicianPayout
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int TechnicianPaymentId { get; set; }

        [Required]
        public int TechnicianId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [StringLength(3)]
        public string Currency { get; set; } = "MYR";

        [StringLength(40)]
        public string PayoutProvider { get; set; } = "Xendit";

        [StringLength(120)]
        public string? ProviderPayoutId { get; set; }

        [StringLength(20)]
        public string PayoutStatus { get; set; } = TechnicianPayoutStatuses.Pending;

        [StringLength(80)]
        public string PayoutReference { get; set; } = string.Empty;

        public DateTime? PaidOutAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [ForeignKey(nameof(TechnicianPaymentId))]
        public TechnicianPayment TechnicianPayment { get; set; } = null!;

        [ForeignKey(nameof(TechnicianId))]
        public TechnicianProfile Technician { get; set; } = null!;
    }
}
