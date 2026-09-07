using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudMVCApplication.Models
{
    public class Message
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MessageId { get; set; }

        [Required]
        public int ConversationId { get; set; }

        [Required]
        public string SenderUserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(4000)]
        public string Body { get; set; } = string.Empty;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; }

        [ForeignKey(nameof(ConversationId))]
        public Conversation Conversation { get; set; } = null!;

        [ForeignKey(nameof(SenderUserId))]
        public ApplicationUser Sender { get; set; } = null!;
    }
}
