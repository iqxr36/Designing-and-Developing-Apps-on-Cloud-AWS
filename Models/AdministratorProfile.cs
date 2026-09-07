using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{
    /// <summary>
    /// Profile details for administrator users. Shared name, email, and phone live on ApplicationUser.
    /// </summary>
    public class AdministratorProfile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AdministratorId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public string Department { get; set; } = "Administration";

        public string AccessLevel { get; set; } = "Company Administrator";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(UserId))]
        public ApplicationUser User { get; set; } = null!;
    }
}