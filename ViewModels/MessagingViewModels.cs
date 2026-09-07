using CloudMVCApplication.Models;
using CloudMVCApplication.ViewModels;

namespace CloudMVCApplication.ViewModels
{
    public record MessageViewModel(
        int MessageId,
        string SenderName,
        string Body,
        DateTime SentAt,
        bool IsMine);

    public record ConversationListItemViewModel(
        int ConversationId,
        int RequestId,
        string RequestTitle,
        string RequestDisplayId,
        ConversationType Type,
        string TypeLabel,
        string OtherPartyName,
        string? LastMessagePreview,
        DateTime? LastMessageAt,
        int UnreadCount);

    public record ChatViewModel(
        int ConversationId,
        int RequestId,
        string RequestTitle,
        string RequestDisplayId,
        ConversationType Type,
        string TypeLabel,
        string CurrentUserId,
        IReadOnlyList<MessageViewModel> Messages);

    public record MessagingInboxViewModel(
        string CurrentUserId,
        int TotalUnreadCount,
        IReadOnlyList<ConversationListItemViewModel> Conversations);
}
