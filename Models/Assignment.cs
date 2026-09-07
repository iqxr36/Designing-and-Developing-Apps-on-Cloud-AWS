using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{
    public class Assignment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AssignmentId { get; set; }

        [Required]
        public int RequestId { get; set; }

        [Required]
        public int TechnicianId { get; set; }

        [Required]
        public string AssignedByManagerId { get; set; } = string.Empty;

        public DateTime AssignedDate { get; set; } = DateTime.UtcNow;

        public string Status { get; set; } = "Assigned";

        public string? CompletionNotes { get; set; }

        public DateTime? CompletedAt { get; set; }

        [ForeignKey(nameof(RequestId))]
        public MaintenanceRequest Request { get; set; } = null!;

        [ForeignKey(nameof(TechnicianId))]
        public TechnicianProfile Technician { get; set; } = null!;

        [ForeignKey(nameof(AssignedByManagerId))]
        public ApplicationUser AssignedByManager { get; set; } = null!;

        public ServicePayment? ServicePayment { get; set; }
    }
}
