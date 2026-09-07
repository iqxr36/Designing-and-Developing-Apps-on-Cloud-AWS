using CloudMVCApplication.Data;
using CloudMVCApplication.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CloudMVCApplication.Services
{
    public class XenditSubscriptionService
    {
        private readonly ApplicationDbContext _context;
        private readonly IXenditPaymentService _xendit;
        private readonly XenditSettings _settings;
        private readonly CompanyBillingHistoryService _billingHistory;

        public XenditSubscriptionService(
            ApplicationDbContext context,
            IXenditPaymentService xendit,
            IOptions<XenditSettings> settings,
            CompanyBillingHistoryService billingHistory)
        {
            _context = context;
            _xendit = xendit;
            _settings = settings.Value;
            _billingHistory = billingHistory;
        }

        public bool IsConfigured => _settings.IsConfigured;

        public async Task<string?> GetOrCreatePendingCheckoutUrlAsync(int companyId)
        {
            var payment = await _context.SubscriptionPayments
                .Include(p => p.Company)
                .Include(p => p.CompanySubscription)
                    .ThenInclude(s => s.SubscriptionPlan)
                .Where(p => p.CompanyId == companyId && p.PaymentStatus == PaymentStatuses.Pending)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (payment != null)
            {
                if (!string.IsNullOrWhiteSpace(payment.CheckoutUrl))
                {
                    return payment.CheckoutUrl;
                }

                var invoice = await _xendit.CreateSubscriptionInvoiceAsync(payment);
                payment.ProviderInvoiceId = invoice.ProviderInvoiceId;
                payment.CheckoutUrl = invoice.CheckoutUrl;
                payment.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return payment.CheckoutUrl;
            }

            var company = await _context.PropertyManagementCompanies.FirstOrDefaultAsync(c => c.CompanyId == companyId);
            if (company == null)
            {
                return null;
            }

            return await CreateCheckoutAsync(companyId, company.SubscriptionPlan);
        }

        public async Task<string?> CreateCheckoutAsync(int companyId, string planName)
        {
            var normalizedPlan = SubscriptionPlans.Normalize(planName);
            var company = await _context.PropertyManagementCompanies.FirstOrDefaultAsync(c => c.CompanyId == companyId);
            if (company == null)
            {
                return null;
            }

            var plan = await GetOrCreatePlanAsync(normalizedPlan);
            var now = DateTime.UtcNow;
            var subscription = new CompanySubscription
            {
                CompanyId = company.CompanyId,
                SubscriptionPlanId = plan.Id,
                Status = CompanySubscriptionRecordStatuses.Pending,
                PaymentStatus = PaymentStatuses.Pending,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.CompanySubscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            var payment = new SubscriptionPayment
            {
                CompanySubscriptionId = subscription.Id,
                CompanyId = company.CompanyId,
                GrossAmount = plan.Price,
                Currency = plan.Currency,
                PaymentProvider = "Xendit",
                PaymentMethod = "FPX",
                PaymentStatus = PaymentStatuses.Pending,
                TransactionReference = GenerateTransactionReference(company.CompanyId, now),
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.SubscriptionPayments.Add(payment);
            company.SubscriptionPlan = plan.Name;
            company.SubscriptionStatus = CompanySubscriptionStatuses.PendingPayment;
            company.MaxProperties = plan.MaxProperties;
            company.MaxUnits = plan.MaxTenants;
            company.MaxManagers = plan.MaxManagers;
            await _context.SaveChangesAsync();

            payment.Company = company;
            payment.CompanySubscription = subscription;
            subscription.SubscriptionPlan = plan;
            var invoice = await _xendit.CreateSubscriptionInvoiceAsync(payment);

            payment.ProviderInvoiceId = invoice.ProviderInvoiceId;
            payment.CheckoutUrl = invoice.CheckoutUrl;
            payment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return payment.CheckoutUrl;
        }

        public async Task<SubscriptionPlan> GetOrCreatePlanAsync(string planName)
        {
            var normalizedPlan = SubscriptionPlans.Normalize(planName);
            var existing = await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Name == normalizedPlan);
            if (existing != null)
            {
                return existing;
            }

            var limits = SubscriptionPlans.GetLimits(normalizedPlan);
            var plan = new SubscriptionPlan
            {
                Name = normalizedPlan,
                Description = SubscriptionPlans.GetPlanDescription(normalizedPlan),
                Price = SubscriptionPlans.GetMonthlyPrice(normalizedPlan),
                Currency = "MYR",
                BillingCycle = "Monthly",
                MaxProperties = limits.MaxProperties,
                MaxManagers = limits.MaxManagers,
                MaxTenants = limits.MaxUnits,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.SubscriptionPlans.Add(plan);
            await _context.SaveChangesAsync();
            return plan;
        }

        public async Task<bool> SuspendSubscriptionAsync(int companyId)
        {
            var company = await _context.PropertyManagementCompanies.FirstOrDefaultAsync(c => c.CompanyId == companyId);
            if (company == null)
            {
                return false;
            }

            company.Status = "Suspended";
            company.SubscriptionStatus = CompanySubscriptionStatuses.Suspended;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ReactivateCompanyAsync(int companyId)
        {
            var company = await _context.PropertyManagementCompanies.FirstOrDefaultAsync(c => c.CompanyId == companyId);
            if (company == null)
            {
                return false;
            }

            company.Status = "Active";
            if (company.SubscriptionStatus == CompanySubscriptionStatuses.Suspended)
            {
                company.SubscriptionStatus = CompanySubscriptionStatuses.Active;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ChangePlanAsync(int companyId, string newPlan)
        {
            var normalizedPlan = SubscriptionPlans.Normalize(newPlan);
            var company = await _context.PropertyManagementCompanies.FirstOrDefaultAsync(c => c.CompanyId == companyId);
            if (company == null)
            {
                return false;
            }

            var plan = await GetOrCreatePlanAsync(normalizedPlan);
            var previousPlan = company.SubscriptionPlan;
            company.SubscriptionPlan = plan.Name;
            company.MaxProperties = plan.MaxProperties;
            company.MaxUnits = plan.MaxTenants;
            company.MaxManagers = plan.MaxManagers;
            await _context.SaveChangesAsync();

            if (!string.Equals(previousPlan, plan.Name, StringComparison.Ordinal))
            {
                await _billingHistory.RecordEventAsync(companyId, CompanySubscriptionEventTypes.PlanChanged, previousPlan: previousPlan, newPlan: plan.Name);
            }

            return true;
        }

        public async Task<bool> MarkSubscriptionActiveManuallyAsync(int companyId)
        {
            var company = await _context.PropertyManagementCompanies.FirstOrDefaultAsync(c => c.CompanyId == companyId);
            if (company == null)
            {
                return false;
            }

            company.SubscriptionStatus = CompanySubscriptionStatuses.Active;
            company.Status = "Active";
            company.TrialEndsAt = null;
            await _context.SaveChangesAsync();
            return true;
        }

        private static string GenerateTransactionReference(int companyId, DateTime createdAt)
            => $"SUB-{createdAt:yyyyMMddHHmmss}-{companyId:0000}";
    }
}
