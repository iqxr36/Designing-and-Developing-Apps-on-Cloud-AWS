using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{
    public class TechnicianDocument
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TechnicianId { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FileType { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(TechnicianId))]
        public TechnicianProfile Technician { get; set; } = null!;
    }
}
