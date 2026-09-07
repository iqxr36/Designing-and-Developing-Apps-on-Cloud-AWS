using CloudMVCApplication.Data;
using CloudMVCApplication.Models;
using CloudMVCApplication.Services;
using CloudMVCApplication.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CloudMVCApplication.Areas.Technician.Controllers
{
    [Authorize(Roles = "Technician")]
    [Area("Technician")]
    public class EarningsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EarningsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var payments = await _context.TechnicianPayments
                .Include(p => p.MaintenanceRequest)
                    .ThenInclude(r => r.Property)
                .Include(p => p.MaintenanceRequest)
                    .ThenInclude(r => r.Unit)
                .Include(p => p.Technician)
                .Include(p => p.Payout)
                .Where(p => p.Technician.UserId == CurrentUserId)
                .OrderByDescending(p => p.PaidAt ?? p.CreatedAt)
                .Select(p => new
                {
                    p.Id,
                    RequestId = p.MaintenanceRequestId,
                    p.MaintenanceRequest.Title,
                    p.MaintenanceRequest.Property.PropertyName,
                    p.MaintenanceRequest.Unit.UnitNumber,
                    p.GrossAmount,
                    p.PlatformCommissionAmount,
                    p.TechnicianNetAmount,
                    p.PaymentStatus,
                    TechnicianPayoutStatus = p.Payout == null ? null : p.Payout.PayoutStatus,
                    p.CreatedAt,
                    p.PaidAt,
                    p.TransactionReference
                })
                .ToListAsync();

            var rows = payments.Select(p => new TechnicianPaymentRowViewModel(
                p.Id,
                p.RequestId,
                $"REQ-{p.RequestId:0000}",
                p.Title,
                p.PropertyName,
                p.UnitNumber,
                p.GrossAmount,
                p.PlatformCommissionAmount,
                p.TechnicianNetAmount,
                p.PaymentStatus,
                p.TechnicianPayoutStatus,
                p.PaymentStatus == TechnicianPaymentStatuses.Paid && p.TechnicianPayoutStatus == TechnicianPayoutStatuses.Pending
                    ? "Pending payout"
                    : p.PaymentStatus,
                p.CreatedAt,
                p.PaidAt,
                null,
                p.TransactionReference))
                .ToList();

            var model = new TechnicianEarningsViewModel
            {
                Payments = rows,
                TotalPaidToBank = rows
                    .Where(p => p.PaymentStatus == TechnicianPaymentStatuses.Paid)
                    .Sum(p => p.TechnicianNetAmount),
                AwaitingTransferAmount = rows
                    .Where(p => p.PaymentStatus == TechnicianPaymentStatuses.Paid)
                    .Sum(p => p.PlatformFeeAmount),
                PendingReleaseAmount = rows
                    .Where(p => p.PaymentStatus == TechnicianPaymentStatuses.Pending)
                    .Sum(p => p.TechnicianNetAmount),
                PaidToBankJobs = rows.Count(p => p.PaymentStatus == TechnicianPaymentStatuses.Paid),
                LatestBankPayoutAt = rows
                    .Where(p => p.PaidAt.HasValue)
                    .Select(p => p.PaidAt)
                    .Max()
            };

            return View(model);
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    }
}
