using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace CloudMVCApplication.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        public string FullName { get; set; } = string.Empty;
        
        public int? CompanyId { get; set; }

        public PropertyManagementCompany? Company { get; set; }

        public bool IsActive { get; set; } = true;

        public string? AvatarUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public TenantProfile? TenantProfile { get; set; }

        public TechnicianProfile? TechnicianProfile { get; set; }

        public AdministratorProfile? AdministratorProfile { get; set; }

        public ManagerProfile? ManagerProfile { get; set; }

        public ICollection<RequestImage> UploadedRequestImages { get; set; } = new List<RequestImage>();

        public ICollection<RequestStatusHistory> RequestStatusChanges { get; set; } = new List<RequestStatusHistory>();

        public ICollection<Assignment> ManagerAssignments { get; set; } = new List<Assignment>();

        public ICollection<ServicePayment> RecordedServicePayments { get; set; } = new List<ServicePayment>();

        public ICollection<TechnicianPayment> RecordedTechnicianPayments { get; set; } = new List<TechnicianPayment>();

        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

        public ICollection<ConversationParticipant> ConversationParticipants { get; set; } = new List<ConversationParticipant>();

        public ICollection<Message> SentMessages { get; set; } = new List<Message>();
    }
}
