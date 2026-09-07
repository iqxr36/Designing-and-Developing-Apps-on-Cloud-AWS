using CloudMVCApplication.Services;
using CloudMVCApplication.Data;
using CloudMVCApplication.Models;
using CloudMVCApplication.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CloudMVCApplication.Areas.Technician.Controllers
{
    [Authorize(Roles = "Technician")]
    [Area("Technician")]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly DashboardDataService _dashboardData;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly TechnicianPayoutService _payoutService;
        private readonly IAvatarService _avatarService;

        public SettingsController(
            ApplicationDbContext context,
            DashboardDataService dashboardData,
            UserManager<ApplicationUser> userManager,
            TechnicianPayoutService payoutService,
            IAvatarService avatarService)
        {
            _context = context;
            _dashboardData = dashboardData;
            _userManager = userManager;
            _payoutService = payoutService;
            _avatarService = avatarService;
        }

        public async Task<IActionResult> Index()
        {
            var model = await _dashboardData.GetTechnicianDashboardAsync(CurrentUserId);
            var technician = await LoadCurrentTechnicianAsync();
            model.PayoutStatus = technician == null
                ? new TechnicianPayoutStatusViewModel()
                : await _payoutService.GetPayoutStatusAsync(technician.TechnicianId);
            return View(model);
        }

        public async Task<IActionResult> ChangeAvatar()
        {
            var model = await _dashboardData.GetTechnicianDashboardAsync(CurrentUserId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(52_428_800)]
        public async Task<IActionResult> UpdateAvatar(IFormFile? avatarFile, string? avatarChoice)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var (succeeded, errorMessage) = await _avatarService.UpdateAsync(
                currentUser,
                avatarFile,
                avatarChoice,
                HttpContext.RequestAborted);

            if (!succeeded)
            {
                ModelState.AddModelError(string.Empty, errorMessage ?? "Unable to update avatar.");
                return View("ChangeAvatar", await _dashboardData.GetTechnicianDashboardAsync(CurrentUserId));
            }

            TempData["SuccessMessage"] = "Avatar updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAvatar()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            await _avatarService.RemoveAsync(currentUser, HttpContext.RequestAborted);
            TempData["SuccessMessage"] = "Avatar removed successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(TechnicianSettingsUpdateViewModel input)
        {
            var user = await _userManager.GetUserAsync(User);
            var technician = await LoadCurrentTechnicianAsync();
            if (user == null || technician == null)
            {
                return Challenge();
            }

            if (!ModelState.IsValid)
            {
                return await IndexViewWithPayoutStatusAsync();
            }

            user.FullName = input.FullName.Trim();
            user.PhoneNumber = input.PhoneNumber;
            technician.Specialization = input.Specialization.Trim();
            technician.AvailabilityStatus = input.AvailabilityStatus.Trim();

            if (!string.Equals(user.Email, input.Email, StringComparison.OrdinalIgnoreCase))
            {
                await _userManager.SetEmailAsync(user, input.Email);
                await _userManager.SetUserNameAsync(user, input.Email);
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return await IndexViewWithPayoutStatusAsync();
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Settings updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePassword(TechnicianPasswordUpdateViewModel input)
        {
            if (!ModelState.IsValid)
            {
                return await IndexViewWithPayoutStatusAsync();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var result = await _userManager.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return await IndexViewWithPayoutStatusAsync();
            }

            TempData["SuccessMessage"] = "Password updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePayoutDetails(TechnicianPayoutDetailsViewModel input)
        {
            var technician = await LoadCurrentTechnicianAsync();
            if (technician == null)
            {
                return Challenge();
            }

            if (!ModelState.IsValid)
            {
                return await IndexViewWithPayoutStatusAsync();
            }

            var (success, error) = await _payoutService.SaveBankDetailsAsync(technician.TechnicianId, input);
            if (!success)
            {
                TempData["ErrorMessage"] = error;
                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] = "Bank payout details saved. Net earnings are paid by bank transfer after managers release payment and the platform fee is deducted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> IndexViewWithPayoutStatusAsync()
        {
            var model = await _dashboardData.GetTechnicianDashboardAsync(CurrentUserId);
            var technician = await LoadCurrentTechnicianAsync();
            model.PayoutStatus = technician == null
                ? new TechnicianPayoutStatusViewModel()
                : await _payoutService.GetPayoutStatusAsync(technician.TechnicianId);
            return View("Index", model);
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        private async Task<TechnicianProfile?> LoadCurrentTechnicianAsync()
        {
            return await _context.TechnicianProfiles
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.UserId == CurrentUserId);
        }
    }
}
