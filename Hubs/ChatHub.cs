using CloudMVCApplication.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace CloudMVCApplication.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private static readonly ConcurrentDictionary<string, DateTime> LastSendTimes = new();

        private readonly MessagingService _messagingService;

        public ChatHub(MessagingService messagingService)
        {
            _messagingService = messagingService;
        }

        public async Task JoinInbox()
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new HubException("Authentication required.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        }

        public async Task JoinConversation(int conversationId)
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId) || !await _messagingService.IsParticipantAsync(conversationId, userId))
            {
                throw new HubException("You are not allowed to join this conversation.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(conversationId));
        }

        public async Task MarkAsRead(int conversationId)
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId) || !await _messagingService.IsParticipantAsync(conversationId, userId))
            {
                return;
            }

            await _messagingService.MarkAsReadAsync(conversationId, userId);
        }

        public async Task SendMessage(int conversationId, string body)
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new HubException("Authentication required.");
            }

            if (IsRateLimited(userId))
            {
                throw new HubException("Please wait a moment before sending another message.");
            }

            var message = await _messagingService.SendMessageAsync(conversationId, userId, body);
            if (message == null)
            {
                throw new HubException("Message could not be sent.");
            }

            LastSendTimes[userId] = DateTime.UtcNow;
            await Clients.OthersInGroup(GroupName(conversationId)).SendAsync("ReceiveMessage", new
            {
                message.MessageId,
                message.SenderName,
                message.Body,
                message.SentAt,
                isMine = false
            });
            await Clients.Caller.SendAsync("ReceiveMessage", message);

            await PushInboxUpdatesAsync(conversationId);
        }

        public async Task UserTyping(int conversationId)
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId) || !await _messagingService.IsParticipantAsync(conversationId, userId))
            {
                return;
            }

            await Clients.OthersInGroup(GroupName(conversationId)).SendAsync("UserTyping", userId);
        }

        private async Task PushInboxUpdatesAsync(int conversationId)
        {
            var participantIds = await _messagingService.GetParticipantUserIdsAsync(conversationId);
            foreach (var participantId in participantIds)
            {
                var listItem = await _messagingService.GetConversationListItemAsync(conversationId, participantId);
                if (listItem == null)
                {
                    continue;
                }

                await Clients.Group(UserGroup(participantId)).SendAsync("InboxUpdated", listItem);
            }
        }

        private string? GetUserId() => Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        private static string GroupName(int conversationId) => $"conv-{conversationId}";

        private static string UserGroup(string userId) => $"user-{userId}";

        private static bool IsRateLimited(string userId)
        {
            if (!LastSendTimes.TryGetValue(userId, out var lastSend))
            {
                return false;
            }

            return DateTime.UtcNow - lastSend < TimeSpan.FromSeconds(1);
        }
    }
}
