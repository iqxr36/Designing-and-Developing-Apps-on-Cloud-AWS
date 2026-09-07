using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{
    public class Property
    {
        [Key]
        public int PropertyId { get; set; }

        [Required]
        public int CompanyId { get; set; }

        [Required]
        public string PropertyName { get; set; } = string.Empty;

        public string? Address { get; set; }

        public string? City { get; set; }

        public string PropertyType { get; set; } = "Residential Building";

        public string? ImageUrl { get; set; }

        public string Status { get; set; } = "Active";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(CompanyId))]
        public PropertyManagementCompany Company { get; set; } = null!;

        public ICollection<Unit> Units { get; set; } = new List<Unit>();

        public ICollection<ManagerProfile> Managers { get; set; } = new List<ManagerProfile>();

        public ICollection<MaintenanceRequest> MaintenanceRequests { get; set; } = new List<MaintenanceRequest>();
    }
}
