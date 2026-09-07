using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{
    public class RequestStatusHistory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int HistoryId { get; set; }

        [Required]
        public int RequestId { get; set; }

        [Required]
        public string ChangedByUserId { get; set; } = string.Empty;

        public string? OldStatus { get; set; }

        public string? NewStatus { get; set; }

        public string? Notes { get; set; }

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(RequestId))]
        public MaintenanceRequest Request { get; set; } = null!;

        [ForeignKey(nameof(ChangedByUserId))]
        public ApplicationUser ChangedByUser { get; set; } = null!;
    }
}
