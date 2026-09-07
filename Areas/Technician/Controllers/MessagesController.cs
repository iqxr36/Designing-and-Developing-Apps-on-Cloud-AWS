using CloudMVCApplication.Models;
using CloudMVCApplication.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CloudMVCApplication.Areas.Technician.Controllers
{
    [Authorize(Roles = "Technician")]
    [Area("Technician")]
    public class MessagesController : Controller
    {
        private readonly MessagingService _messagingService;

        public MessagesController(MessagingService messagingService)
        {
            _messagingService = messagingService;
        }

        public async Task<IActionResult> Index()
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            return View(await _messagingService.GetInboxAsync(userId, "Technician"));
        }

        public async Task<IActionResult> Chat(int id)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var chat = await _messagingService.GetConversationAsync(id, userId);
            if (chat == null)
            {
                return Forbid();
            }

            await _messagingService.MarkAsReadAsync(id, userId);
            return View(chat);
        }

        public async Task<IActionResult> ChatByRequest(int requestId)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var conversationId = await _messagingService.GetConversationIdByRequestAsync(requestId, ConversationType.JobCoordination, userId);
            if (!conversationId.HasValue)
            {
                return NotFound();
            }

            return RedirectToAction(nameof(Chat), new { id = conversationId.Value });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(int id, string body)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            await _messagingService.SendMessageAsync(id, userId, body);
            return RedirectToAction(nameof(Chat), new { id });
        }

        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
