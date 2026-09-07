using CloudMVCApplication.Data;
using CloudMVCApplication.Models;
using CloudMVCApplication.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CloudMVCApplication.Services
{
    public class TechnicianPayoutService
    {
        private readonly ApplicationDbContext _context;

        public TechnicianPayoutService(ApplicationDbContext context)
        {
            _context = context;
        }

        public bool IsPayoutReady(TechnicianProfile technician) =>
            technician.User.IsActive &&
            !string.IsNullOrWhiteSpace(technician.PayoutAccountHolderName) &&
            !string.IsNullOrWhiteSpace(technician.PayoutBankName) &&
            !string.IsNullOrWhiteSpace(technician.PayoutAccountNumber);

        public async Task<TechnicianPayoutStatusViewModel> GetPayoutStatusAsync(int technicianId)
        {
            var technician = await _context.TechnicianProfiles
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TechnicianId == technicianId);

            if (technician == null)
            {
                return new TechnicianPayoutStatusViewModel();
            }

            var canSave = technician.User.IsActive;
            var isReady = IsPayoutReady(technician);

            return new TechnicianPayoutStatusViewModel
            {
                IsReady = isReady,
                CanSaveDetails = canSave,
                AccountHolderName = technician.PayoutAccountHolderName,
                BankName = technician.PayoutBankName,
                AccountNumberMasked = MaskAccountNumber(technician.PayoutAccountNumber),
                SetupCompletedAt = technician.PayoutSetupCompletedAt,
                BlockReason = canSave
                    ? null
                    : "Your account must be approved before saving bank payout details."
            };
        }

        public async Task<(bool Success, string? Error)> SaveBankDetailsAsync(
            int technicianId,
            TechnicianPayoutDetailsViewModel input)
        {
            var technician = await _context.TechnicianProfiles
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TechnicianId == technicianId);

            if (technician == null)
            {
                return (false, "Technician profile not found.");
            }

            if (!technician.User.IsActive)
            {
                return (false, "Your account must be approved before saving bank payout details.");
            }

            var holderName = input.AccountHolderName?.Trim();
            var bankName = input.BankName?.Trim();
            var accountNumber = input.AccountNumber?.Trim();

            if (string.IsNullOrWhiteSpace(holderName) ||
                string.IsNullOrWhiteSpace(bankName) ||
                string.IsNullOrWhiteSpace(accountNumber))
            {
                return (false, "Enter account holder name, bank name, and account number.");
            }

            if (accountNumber.Length < 6)
            {
                return (false, "Enter a valid bank account number.");
            }

            technician.PayoutAccountHolderName = holderName;
            technician.PayoutBankName = bankName;
            technician.PayoutAccountNumber = accountNumber;
            technician.PayoutSetupCompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return (true, null);
        }

        public static string? MaskAccountNumber(string? accountNumber)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
            {
                return null;
            }

            var trimmed = accountNumber.Trim();
            return trimmed.Length <= 4
                ? new string('*', trimmed.Length)
                : $"{new string('*', trimmed.Length - 4)}{trimmed[^4..]}";
        }

        public async Task<(bool Success, string? Error)> MarkTransferredAsync(
            int payoutId,
            string superAdminUserId,
            string? reference)
        {
            var payout = await _context.TechnicianPayouts
                .Include(p => p.TechnicianPayment)
                .FirstOrDefaultAsync(p => p.Id == payoutId);

            if (payout == null)
            {
                return (false, "Payout not found.");
            }

            if (payout.TechnicianPayment.PaymentStatus != TechnicianPaymentStatuses.Paid)
            {
                return (false, "Technician payment must be completed before marking payout paid.");
            }

            if (payout.PayoutStatus != TechnicianPayoutStatuses.Pending)
            {
                return (false, "This payout is not pending.");
            }

            payout.PayoutStatus = TechnicianPayoutStatuses.Paid;
            payout.PaidOutAt = DateTime.UtcNow;
            payout.PayoutReference = string.IsNullOrWhiteSpace(reference)
                ? payout.PayoutReference
                : reference.Trim();

            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<List<TechnicianPayout>> GetPendingTransfersAsync()
        {
            return await _context.TechnicianPayouts
                .AsNoTracking()
                .Include(p => p.TechnicianPayment)
                    .ThenInclude(p => p.MaintenanceRequest)
                        .ThenInclude(r => r.Property)
                            .ThenInclude(p => p.Company)
                .Include(p => p.Technician)
                    .ThenInclude(t => t.User)
                .Where(p => p.PayoutStatus == TechnicianPayoutStatuses.Pending)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<int> GetPendingTransferCountAsync()
        {
            return await _context.TechnicianPayouts.CountAsync(p => p.PayoutStatus == TechnicianPayoutStatuses.Pending);
        }
    }
}
