using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{
    public class SubscriptionPlan
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(80)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [StringLength(3)]
        public string Currency { get; set; } = "MYR";

        [StringLength(20)]
        public string BillingCycle { get; set; } = "Monthly";

        public int MaxProperties { get; set; }

        public int MaxManagers { get; set; }

        public int MaxTenants { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<CompanySubscription> CompanySubscriptions { get; set; } = new List<CompanySubscription>();
    }
}
