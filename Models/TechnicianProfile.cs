using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{

    public class TechnicianProfile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TechnicianId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public string Specialization { get; set; } = "General Maintenance";

        public int ExperienceYears { get; set; }

        public string? ServiceArea { get; set; }

        public string AvailabilityStatus { get; set; } = "Available";

        [Column(TypeName = "decimal(3,2)")]
        public decimal AverageRating { get; set; }

        public int TotalCompletedJobs { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? ProviderConnectAccountId { get; set; }

        public bool ConnectOnboardingComplete { get; set; }

        public bool ConnectChargesEnabled { get; set; }

        public bool ConnectPayoutsEnabled { get; set; }

        public string? PayoutAccountHolderName { get; set; }

        public string? PayoutBankName { get; set; }

        public string? PayoutAccountNumber { get; set; }

        public DateTime? PayoutSetupCompletedAt { get; set; }

        [ForeignKey(nameof(UserId))]
        public ApplicationUser User { get; set; } = null!;

        public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();

        public ICollection<ServicePayment> ServicePayments { get; set; } = new List<ServicePayment>();

        public ICollection<TechnicianPayment> TechnicianPayments { get; set; } = new List<TechnicianPayment>();

        public ICollection<TechnicianDocument> UploadedDocuments { get; set; } = new List<TechnicianDocument>();

        public ICollection<RequestFeedback> RequestFeedbacks { get; set; } = new List<RequestFeedback>();
    }
}
