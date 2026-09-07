using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{

    public class TenantProfile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TenantId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public int UnitId { get; set; }

        public DateTime? MoveInDate { get; set; }

        public string Status { get; set; } = "Active";

        [ForeignKey(nameof(UserId))]
        public ApplicationUser User { get; set; } = null!;

        [ForeignKey(nameof(UnitId))]
        public Unit Unit { get; set; } = null!;

        public ICollection<MaintenanceRequest> MaintenanceRequests { get; set; } = new List<MaintenanceRequest>();

        public ICollection<RequestFeedback> RequestFeedbacks { get; set; } = new List<RequestFeedback>();
    }
}