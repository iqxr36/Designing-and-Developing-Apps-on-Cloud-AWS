using CloudMVCApplication.Data;
using CloudMVCApplication.Models;
using CloudMVCApplication.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CloudMVCApplication.Services
{
    public class TechnicianPaymentService
    {
        private const decimal DefaultGrossAmount = 100m;
        private readonly ApplicationDbContext _context;
        private readonly IXenditPaymentService _xendit;
        private readonly XenditSettings _settings;

        public TechnicianPaymentService(
            ApplicationDbContext context,
            IXenditPaymentService xendit,
            IOptions<XenditSettings> settings)
        {
            _context = context;
            _xendit = xendit;
            _settings = settings.Value;
        }

        public async Task<ManagerPaymentReleaseViewModel?> BuildPaymentReleaseViewModelAsync(int requestId, int? companyId)
        {
            var request = await _context.MaintenanceRequests
                .Include(r => r.Property)
                .Include(r => r.Unit)
                .Include(r => r.Assignment)
                    .ThenInclude(a => a!.Technician)
                        .ThenInclude(t => t.User)
                .Include(r => r.TechnicianPayment)
                .FirstOrDefaultAsync(r => r.RequestId == requestId);

            if (request == null || request.Status != "Completed" || request.Assignment == null)
            {
                return null;
            }

            if (companyId.HasValue && request.Property.CompanyId != companyId.Value)
            {
                return null;
            }

            var payment = request.TechnicianPayment;
            var grossAmount = payment?.GrossAmount ?? DefaultGrossAmount;
            var commission = CalculateCommission(grossAmount);
            var canRelease = payment == null ||
                payment.PaymentStatus is TechnicianPaymentStatuses.Failed or TechnicianPaymentStatuses.Expired or TechnicianPaymentStatuses.Cancelled;

            return new ManagerPaymentReleaseViewModel
            {
                RequestId = request.RequestId,
                PaymentId = payment?.Id,
                Amount = payment?.GrossAmount,
                DefaultAmount = DefaultGrossAmount,
                PlatformFeeAmount = payment?.PlatformCommissionAmount ?? commission,
                TechnicianNetAmount = payment?.TechnicianNetAmount ?? grossAmount - commission,
                PlatformFeePercent = TechnicianPayment.CommissionRate,
                PaymentStatus = payment?.PaymentStatus,
                FailureReason = payment?.PaymentStatus == TechnicianPaymentStatuses.Failed ? "Xendit payment failed." : null,
                PaidAt = payment?.PaidAt,
                CompanyHasBilling = true,
                TechnicianPayoutReady = true,
                CanRelease = canRelease,
                BlockReason = canRelease ? null : payment?.PaymentStatus == TechnicianPaymentStatuses.Pending
                    ? "Payment invoice has already been created. Complete checkout or wait for Xendit confirmation."
                    : "Payment has already been recorded for this completed job.",
                RequestTitle = request.Title,
                RequestDescription = request.Description,
                TechnicianName = request.Assignment.Technician.User.FullName,
                PropertyName = request.Property.PropertyName,
                UnitNumber = request.Unit.UnitNumber,
                Currency = payment?.Currency ?? _settings.Currency,
                PaymentMethod = payment?.PaymentMethod ?? "FPX",
                Notes = payment?.Notes,
                TransactionReference = payment?.TransactionReference,
                CheckoutUrl = payment?.CheckoutUrl
            };
        }

        public async Task<(TechnicianPayment? Payment, string? CheckoutUrl, string? Error)> CreateXenditPaymentAsync(
            int requestId,
            decimal grossAmount,
            string paymentMethod,
            string? notes,
            string managerId,
            int? managerCompanyId,
            string payerEmail,
            string successUrl,
            string failureUrl)
        {
            if (grossAmount <= 0)
            {
                return (null, null, "Gross amount must be greater than 0.");
            }

            var request = await _context.MaintenanceRequests
                .Include(r => r.Property)
                .Include(r => r.Assignment)
                    .ThenInclude(a => a!.Technician)
                        .ThenInclude(t => t.User)
                .Include(r => r.TechnicianPayment)
                .FirstOrDefaultAsync(r => r.RequestId == requestId);

            if (request == null)
            {
                return (null, null, "Request not found.");
            }

            if (request.Status != "Completed")
            {
                return (null, null, "Payment can only be made for completed maintenance jobs.");
            }

            if (request.Assignment == null)
            {
                return (null, null, "A technician must be assigned before payment can be made.");
            }

            if (request.TechnicianPayment != null &&
                request.TechnicianPayment.PaymentStatus is not (TechnicianPaymentStatuses.Failed or TechnicianPaymentStatuses.Expired or TechnicianPaymentStatuses.Cancelled))
            {
                return (null, null, "A technician payment already exists for this completed job.");
            }

            if (managerCompanyId.HasValue && request.Property.CompanyId != managerCompanyId.Value)
            {
                return (null, null, "You can only pay for requests under your company.");
            }

            if (request.Assignment.Technician.User.CompanyId.HasValue)
            {
                return (null, null, "Technician accounts must not be linked to a company.");
            }

            var createdAt = DateTime.UtcNow;
            var commission = CalculateCommission(grossAmount);
            var payment = request.TechnicianPayment ?? new TechnicianPayment
            {
                MaintenanceRequestId = request.RequestId,
                ManagerId = managerId,
                TechnicianId = request.Assignment.TechnicianId,
                CompanyId = request.Property.CompanyId,
                CreatedAt = createdAt
            };

            payment.GrossAmount = grossAmount;
            payment.PlatformCommissionRate = TechnicianPayment.CommissionRate;
            payment.PlatformCommissionAmount = commission;
            payment.TechnicianNetAmount = grossAmount - commission;
            payment.Currency = _settings.Currency;
            payment.PaymentProvider = "Xendit";
            payment.PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "FPX" : paymentMethod.Trim();
            payment.PaymentStatus = TechnicianPaymentStatuses.Pending;
            payment.TransactionReference = GenerateTransactionReference(request.RequestId, createdAt);
            payment.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            payment.PaidAt = null;
            payment.UpdatedAt = createdAt;

            if (request.TechnicianPayment == null)
            {
                _context.TechnicianPayments.Add(payment);
                await _context.SaveChangesAsync();
            }

            payment.Manager = await _context.Users.FirstAsync(u => u.Id == managerId);
            var invoice = await _xendit.CreateTechnicianPaymentInvoiceAsync(payment);

            payment.ProviderInvoiceId = invoice.ProviderInvoiceId;
            payment.CheckoutUrl = invoice.CheckoutUrl;
            payment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return (payment, payment.CheckoutUrl, null);
        }

        public async Task MarkPaidFromProviderAsync(TechnicianPayment payment, string? providerPaymentReference, DateTime paidAt)
        {
            if (payment.PaymentStatus == TechnicianPaymentStatuses.Paid)
            {
                return;
            }

            payment.PaymentStatus = TechnicianPaymentStatuses.Paid;
            payment.ProviderPaymentReference = providerPaymentReference;
            payment.PaidAt = paidAt;
            payment.UpdatedAt = paidAt;
            if (string.IsNullOrWhiteSpace(payment.TransactionReference))
            {
                payment.TransactionReference = GenerateTransactionReference(payment.MaintenanceRequestId, paidAt);
            }

            if (!await _context.TechnicianPayouts.AnyAsync(p => p.TechnicianPaymentId == payment.Id))
            {
                _context.TechnicianPayouts.Add(new TechnicianPayout
                {
                    TechnicianPaymentId = payment.Id,
                    TechnicianId = payment.TechnicianId,
                    Amount = payment.TechnicianNetAmount,
                    Currency = payment.Currency,
                    PayoutProvider = "Xendit",
                    PayoutStatus = TechnicianPayoutStatuses.Pending,
                    PayoutReference = $"PO-{DateTime.UtcNow:yyyyMMddHHmmss}-{payment.Id:0000}",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        public static decimal CalculateCommission(decimal grossAmount)
            => Math.Round(grossAmount * 0.10m, 2, MidpointRounding.AwayFromZero);

        private static string GenerateTransactionReference(int requestId, DateTime paidAt)
            => $"TP-{paidAt:yyyyMMddHHmmss}-{requestId:0000}";
    }
}
