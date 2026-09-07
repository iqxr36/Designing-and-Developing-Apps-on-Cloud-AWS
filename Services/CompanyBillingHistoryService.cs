using System.Text.Json;
using CloudMVCApplication.Data;
using CloudMVCApplication.Models;
using Microsoft.EntityFrameworkCore;

namespace CloudMVCApplication.Services
{
    public class CompanyBillingHistoryService
    {
        private readonly ApplicationDbContext _context;

        public CompanyBillingHistoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> HasProcessedProviderEventAsync(string providerEventId)
        {
            if (string.IsNullOrWhiteSpace(providerEventId))
            {
                return false;
            }

            return await _context.CompanySubscriptionEvents
                .AsNoTracking()
                .AnyAsync(e => e.ProviderEventId == providerEventId);
        }

        public async Task RecordEventAsync(
            int companyId,
            string eventType,
            string? providerEventId = null,
            string? providerInvoiceId = null,
            string? previousPlan = null,
            string? newPlan = null,
            string? previousStatus = null,
            string? newStatus = null,
            decimal? amount = null,
            string? currency = null,
            object? payload = null)
        {
            if (!string.IsNullOrWhiteSpace(providerEventId) &&
                await _context.CompanySubscriptionEvents.AnyAsync(e => e.ProviderEventId == providerEventId))
            {
                return;
            }

            _context.CompanySubscriptionEvents.Add(new CompanySubscriptionEvent
            {
                CompanyId = companyId,
                EventType = eventType,
                ProviderEventId = providerEventId,
                ProviderInvoiceId = providerInvoiceId,
                PreviousPlan = previousPlan,
                NewPlan = newPlan,
                PreviousStatus = previousStatus,
                NewStatus = newStatus,
                Amount = amount,
                Currency = currency,
                PayloadJson = payload == null ? null : JsonSerializer.Serialize(payload),
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }

        public Task<List<CompanySubscriptionEvent>> GetRecentEventsAsync(int companyId, int take = 20) =>
            _context.CompanySubscriptionEvents
                .AsNoTracking()
                .Where(e => e.CompanyId == companyId)
                .OrderByDescending(e => e.CreatedAt)
                .Take(take)
                .ToListAsync();

        public Task<List<CompanyInvoice>> GetInvoicesAsync(int companyId, int take = 20) =>
            _context.CompanyInvoices
                .AsNoTracking()
                .Where(i => i.CompanyId == companyId)
                .OrderByDescending(i => i.CreatedAt)
                .Take(take)
                .ToListAsync();
    }
}
