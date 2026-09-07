using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{
    public class RequestFeedback
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int FeedbackId { get; set; }

        [Required]
        public int RequestId { get; set; }

        [Required]
        public int TenantId { get; set; }

        [Required]
        public int TechnicianId { get; set; }

        public int Rating { get; set; }

        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(RequestId))]
        public MaintenanceRequest Request { get; set; } = null!;

        [ForeignKey(nameof(TenantId))]
        public TenantProfile Tenant { get; set; } = null!;

        [ForeignKey(nameof(TechnicianId))]
        public TechnicianProfile Technician { get; set; } = null!;
    }
}
