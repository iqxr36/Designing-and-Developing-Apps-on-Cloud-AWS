using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{
    public class MaintenanceRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RequestId { get; set; }

        [Required]
        public int TenantId { get; set; }

        [Required]
        public int UnitId { get; set; }

        [Required]
        public int PropertyId { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string Category { get; set; } = string.Empty;

        public string Priority { get; set; } = "Medium";

        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey(nameof(TenantId))]
        public TenantProfile Tenant { get; set; } = null!;

        [ForeignKey(nameof(UnitId))]
        public Unit Unit { get; set; } = null!;

        [ForeignKey(nameof(PropertyId))]
        public Property Property { get; set; } = null!;

        public ICollection<RequestImage> RequestImages { get; set; } = new List<RequestImage>();

        public ICollection<RequestStatusHistory> RequestStatusHistories { get; set; } = new List<RequestStatusHistory>();

        public Assignment? Assignment { get; set; }

        public ServicePayment? ServicePayment { get; set; }

        public TechnicianPayment? TechnicianPayment { get; set; }

        public RequestFeedback? RequestFeedback { get; set; }

        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

        public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
    }
}
