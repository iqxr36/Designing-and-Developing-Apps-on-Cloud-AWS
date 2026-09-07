using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{
    public class RequestImage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ImageId { get; set; }

        [Required]
        public int RequestId { get; set; }

        [Required]
        public string UploadedByUserId { get; set; } = string.Empty;

        [Required]
        public string ImageUrl { get; set; } = string.Empty;

        public string ImageType { get; set; } = "IssuePhoto";

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(RequestId))]
        public MaintenanceRequest Request { get; set; } = null!;

        [ForeignKey(nameof(UploadedByUserId))]
        public ApplicationUser UploadedByUser { get; set; } = null!;
    }
}
