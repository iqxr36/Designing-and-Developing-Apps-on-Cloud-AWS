using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{
    public class ConversationParticipant
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ConversationParticipantId { get; set; }

        [Required]
        public int ConversationId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public DateTime? LastReadAt { get; set; }

        [ForeignKey(nameof(ConversationId))]
        public Conversation Conversation { get; set; } = null!;

        [ForeignKey(nameof(UserId))]
        public ApplicationUser User { get; set; } = null!;
    }
}
