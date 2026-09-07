using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{
    public class Unit
    {
        [Key]
        public int UnitId { get; set; }

        [Required]
        public int PropertyId { get; set; }

        [Required]
        public string UnitNumber { get; set; } = string.Empty;

        public int? FloorNumber { get; set; }

        public string UnitType { get; set; } = string.Empty;

        public string Status { get; set; } = "Vacant";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(PropertyId))]
        public Property Property { get; set; } = null!;

        public TenantProfile? TenantProfile { get; set; }

        public ICollection<MaintenanceRequest> MaintenanceRequests { get; set; } = new List<MaintenanceRequest>();
    }
}
