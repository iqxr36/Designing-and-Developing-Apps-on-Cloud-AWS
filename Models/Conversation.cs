using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{
    public class Conversation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ConversationId { get; set; }

        [Required]
        public int RequestId { get; set; }

        [Required]
        public ConversationType Type { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(RequestId))]
        public MaintenanceRequest Request { get; set; } = null!;

        public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();

        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
