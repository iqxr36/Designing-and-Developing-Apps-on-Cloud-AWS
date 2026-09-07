using CloudMVCApplication.Data;
using CloudMVCApplication.Models;
using CloudMVCApplication.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CloudMVCApplication.Services
{
    public class MessagingService
    {
        private const int MaxMessageLength = 4000;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MessagingService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<int?> CreateTenantSupportConversationAsync(int requestId)
        {
            var request = await _context.MaintenanceRequests
                .Include(r => r.Tenant)
                    .ThenInclude(t => t.User)
                .Include(r => r.Property)
                .FirstOrDefaultAsync(r => r.RequestId == requestId);
            if (request == null)
            {
                return null;
            }

            var existing = await _context.Conversations
                .FirstOrDefaultAsync(c => c.RequestId == requestId && c.Type == ConversationType.TenantSupport);
            if (existing != null)
            {
                await EnsureTenantSupportParticipantsAsync(existing.ConversationId, request.PropertyId, request.Property.CompanyId, request.Tenant.UserId);
                return existing.ConversationId;
            }

            var managerUserIds = await ResolveManagerUserIdsAsync(request.PropertyId, request.Property.CompanyId);
            if (managerUserIds.Count == 0)
            {
                return null;
            }

            var conversation = new Conversation
            {
                RequestId = requestId,
                Type = ConversationType.TenantSupport,
                CreatedAt = DateTime.UtcNow
            };

            conversation.Participants.Add(new ConversationParticipant { UserId = request.Tenant.UserId });
            foreach (var managerUserId in managerUserIds)
            {
                conversation.Participants.Add(new ConversationParticipant { UserId = managerUserId });
            }

            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();
            return conversation.ConversationId;
        }

        public async Task SyncAllTenantSupportParticipantsAsync()
        {
            var conversations = await _context.Conversations
                .Include(c => c.Participants)
                .Include(c => c.Request)
                    .ThenInclude(r => r.Property)
                .Include(c => c.Request)
                    .ThenInclude(r => r.Tenant)
                .Where(c => c.Type == ConversationType.TenantSupport)
                .ToListAsync();

            foreach (var conversation in conversations)
            {
                await EnsureTenantSupportParticipantsAsync(
                    conversation.ConversationId,
                    conversation.Request.PropertyId,
                    conversation.Request.Property.CompanyId,
                    conversation.Request.Tenant.UserId,
                    conversation.Participants);
            }

            await _context.SaveChangesAsync();
        }

        public async Task<int?> CreateJobCoordinationConversationAsync(int requestId, string managerUserId, int technicianProfileId)
        {
            var existing = await _context.Conversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.RequestId == requestId && c.Type == ConversationType.JobCoordination);
            if (existing != null)
            {
                return existing.ConversationId;
            }

            var technician = await _context.TechnicianProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TechnicianId == technicianProfileId);
            if (technician == null)
            {
                return null;
            }

            var conversation = new Conversation
            {
                RequestId = requestId,
                Type = ConversationType.JobCoordination,
                CreatedAt = DateTime.UtcNow
            };

            conversation.Participants.Add(new ConversationParticipant { UserId = managerUserId });
            conversation.Participants.Add(new ConversationParticipant { UserId = technician.UserId });

            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();
            return conversation.ConversationId;
        }

        public async Task<MessagingInboxViewModel> GetInboxAsync(string userId, string role)
        {
            var allowedTypes = GetAllowedConversationTypes(role);
            var participantRows = await _context.ConversationParticipants
                .Include(p => p.Conversation)
                    .ThenInclude(c => c.Request)
                .Include(p => p.Conversation)
                    .ThenInclude(c => c.Participants)
                        .ThenInclude(part => part.User)
                .Include(p => p.Conversation)
                    .ThenInclude(c => c.Messages)
                .Where(p => p.UserId == userId && allowedTypes.Contains(p.Conversation.Type))
                .ToListAsync();

            var items = new List<ConversationListItemViewModel>();
            foreach (var participant in participantRows)
            {
                items.Add(BuildConversationListItem(participant, userId));
            }

            return new MessagingInboxViewModel(
                userId,
                items.Sum(i => i.UnreadCount),
                items.OrderByDescending(i => i.LastMessageAt ?? DateTime.MinValue).ToList());
        }

        public async Task<ConversationListItemViewModel?> GetConversationListItemAsync(int conversationId, string userId)
        {
            var participant = await _context.ConversationParticipants
                .Include(p => p.Conversation)
                    .ThenInclude(c => c.Request)
                .Include(p => p.Conversation)
                    .ThenInclude(c => c.Participants)
                        .ThenInclude(part => part.User)
                .Include(p => p.Conversation)
                    .ThenInclude(c => c.Messages)
                .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId);

            return participant == null ? null : BuildConversationListItem(participant, userId);
        }

        public async Task<IReadOnlyList<string>> GetParticipantUserIdsAsync(int conversationId)
        {
            return await _context.ConversationParticipants
                .Where(p => p.ConversationId == conversationId)
                .Select(p => p.UserId)
                .ToListAsync();
        }

        private static ConversationListItemViewModel BuildConversationListItem(ConversationParticipant participant, string userId)
        {
            var conversation = participant.Conversation;
            var lastMessage = conversation.Messages
                .Where(m => !m.IsDeleted)
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefault();
            var unreadCount = conversation.Messages.Count(m =>
                !m.IsDeleted &&
                m.SenderUserId != userId &&
                (participant.LastReadAt == null || m.SentAt > participant.LastReadAt));
            var otherParty = conversation.Participants
                .FirstOrDefault(p => p.UserId != userId)?.User.FullName ?? "Unknown";

            return new ConversationListItemViewModel(
                conversation.ConversationId,
                conversation.RequestId,
                conversation.Request.Title,
                FormatRequestId(conversation.RequestId),
                conversation.Type,
                GetTypeLabel(conversation.Type),
                otherParty,
                lastMessage?.Body,
                lastMessage?.SentAt,
                unreadCount);
        }

        public async Task<ChatViewModel?> GetConversationAsync(int conversationId, string userId)
        {
            if (!await IsParticipantAsync(conversationId, userId))
            {
                return null;
            }

            var conversation = await _context.Conversations
                .Include(c => c.Request)
                .Include(c => c.Messages)
                    .ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync(c => c.ConversationId == conversationId);
            if (conversation == null)
            {
                return null;
            }

            var messages = conversation.Messages
                .Where(m => !m.IsDeleted)
                .OrderBy(m => m.SentAt)
                .Select(m => new MessageViewModel(
                    m.MessageId,
                    m.Sender.FullName,
                    m.Body,
                    m.SentAt,
                    m.SenderUserId == userId))
                .ToList();

            return new ChatViewModel(
                conversation.ConversationId,
                conversation.RequestId,
                conversation.Request.Title,
                FormatRequestId(conversation.RequestId),
                conversation.Type,
                GetTypeLabel(conversation.Type),
                userId,
                messages);
        }

        public async Task<MessageViewModel?> SendMessageAsync(int conversationId, string senderUserId, string body)
        {
            if (!await IsParticipantAsync(conversationId, senderUserId))
            {
                return null;
            }

            body = body?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            if (body.Length > MaxMessageLength)
            {
                body = body[..MaxMessageLength];
            }

            var sender = await _context.Users.FindAsync(senderUserId);
            if (sender == null)
            {
                return null;
            }

            var message = new Message
            {
                ConversationId = conversationId,
                SenderUserId = senderUserId,
                Body = body,
                SentAt = DateTime.UtcNow
            };

            _context.Messages.Add(message);

            var participantIds = await _context.ConversationParticipants
                .Where(p => p.ConversationId == conversationId)
                .Select(p => p.UserId)
                .ToListAsync();

            foreach (var recipientId in participantIds.Where(id => id != senderUserId))
            {
                var conversation = await _context.Conversations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ConversationId == conversationId);
                _context.Notifications.Add(new Notification
                {
                    UserId = recipientId,
                    RequestId = conversation?.RequestId,
                    Title = "New message",
                    Message = $"{sender.FullName}: {TruncatePreview(body)}",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            return new MessageViewModel(
                message.MessageId,
                sender.FullName,
                message.Body,
                message.SentAt,
                true);
        }

        public async Task MarkAsReadAsync(int conversationId, string userId)
        {
            var participant = await _context.ConversationParticipants
                .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId);
            if (participant == null)
            {
                return;
            }

            participant.LastReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            var inbox = await GetInboxAsync(userId, await ResolveRoleAsync(userId));
            return inbox.TotalUnreadCount;
        }

        public async Task<int?> GetConversationIdByRequestAsync(int requestId, ConversationType type, string userId)
        {
            var conversation = await _context.Conversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.RequestId == requestId && c.Type == type);
            if (conversation == null)
            {
                return null;
            }

            return await IsParticipantAsync(conversation.ConversationId, userId)
                ? conversation.ConversationId
                : null;
        }

        public async Task<bool> IsParticipantAsync(int conversationId, string userId)
        {
            return await _context.ConversationParticipants
                .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId);
        }

        private async Task EnsureTenantSupportParticipantsAsync(
            int conversationId,
            int propertyId,
            int companyId,
            string tenantUserId,
            ICollection<ConversationParticipant>? existingParticipants = null)
        {
            existingParticipants ??= await _context.ConversationParticipants
                .Where(p => p.ConversationId == conversationId)
                .ToListAsync();

            var participantIds = existingParticipants.Select(p => p.UserId).ToHashSet();
            if (!participantIds.Contains(tenantUserId))
            {
                _context.ConversationParticipants.Add(new ConversationParticipant
                {
                    ConversationId = conversationId,
                    UserId = tenantUserId
                });
                participantIds.Add(tenantUserId);
            }

            foreach (var managerUserId in await ResolveManagerUserIdsAsync(propertyId, companyId))
            {
                if (!participantIds.Contains(managerUserId))
                {
                    _context.ConversationParticipants.Add(new ConversationParticipant
                    {
                        ConversationId = conversationId,
                        UserId = managerUserId
                    });
                }
            }
        }

        private async Task<List<string>> ResolveManagerUserIdsAsync(int propertyId, int companyId)
        {
            var managerIds = await _context.ManagerProfiles
                .Include(m => m.User)
                .Where(m => m.User.IsActive && m.User.CompanyId == companyId)
                .Where(m => m.PropertyId == null || m.PropertyId == propertyId)
                .Select(m => m.UserId)
                .Distinct()
                .ToListAsync();

            if (managerIds.Count > 0)
            {
                return managerIds;
            }

            var managerUsers = await _userManager.GetUsersInRoleAsync("Manager");
            return managerUsers
                .Where(u => u.IsActive && u.CompanyId == companyId)
                .Select(u => u.Id)
                .Distinct()
                .ToList();
        }

        private async Task<string> ResolveRoleAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return string.Empty;
            }

            var roles = await _userManager.GetRolesAsync(user);
            return roles.FirstOrDefault() ?? string.Empty;
        }

        private static ConversationType[] GetAllowedConversationTypes(string role) => role switch
        {
            "Tenant" => new[] { ConversationType.TenantSupport },
            "Technician" => new[] { ConversationType.JobCoordination },
            "Manager" => new[] { ConversationType.TenantSupport, ConversationType.JobCoordination },
            _ => Array.Empty<ConversationType>()
        };

        private static string GetTypeLabel(ConversationType type) => type switch
        {
            ConversationType.TenantSupport => "Tenant Support",
            ConversationType.JobCoordination => "Job Coordination",
            _ => type.ToString()
        };

        private static string FormatRequestId(int requestId) => $"REQ-{requestId:0000}";

        private static string TruncatePreview(string body) =>
            body.Length <= 80 ? body : body[..77] + "...";
    }
}
