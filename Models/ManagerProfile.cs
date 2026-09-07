using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{

    public class ManagerProfile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ManagerId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public string JobTitle { get; set; } = "Property Manager";

        public string Region { get; set; } = string.Empty;

        public int? PropertyId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(UserId))]
        public ApplicationUser User { get; set; } = null!;

        [ForeignKey(nameof(PropertyId))]
        public Property? AssignedProperty { get; set; }
    }
}
