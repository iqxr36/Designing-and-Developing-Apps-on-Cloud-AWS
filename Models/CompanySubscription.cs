using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{
    public static class CompanySubscriptionRecordStatuses
    {
        public const string Pending = "Pending";
        public const string Active = "Active";
        public const string Expired = "Expired";
        public const string Cancelled = "Cancelled";
    }

    public class CompanySubscription
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int CompanyId { get; set; }

        [Required]
        public int SubscriptionPlanId { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = CompanySubscriptionRecordStatuses.Pending;

        [StringLength(20)]
        public string PaymentStatus { get; set; } = PaymentStatuses.Pending;

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey(nameof(CompanyId))]
        public PropertyManagementCompany Company { get; set; } = null!;

        [ForeignKey(nameof(SubscriptionPlanId))]
        public SubscriptionPlan SubscriptionPlan { get; set; } = null!;

        public ICollection<SubscriptionPayment> Payments { get; set; } = new List<SubscriptionPayment>();
    }
}
