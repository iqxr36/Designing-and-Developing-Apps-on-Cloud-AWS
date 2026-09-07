using CloudMVCApplication.Data;
using CloudMVCApplication.Models;
using CloudMVCApplication.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CloudMVCApplication.Services
{
    public sealed class SubscriptionLimitResult
    {
        public bool Allowed { get; init; }
        public string Message { get; init; } = string.Empty;

        public static SubscriptionLimitResult Success() => new() { Allowed = true };

        public static SubscriptionLimitResult Denied(string message) => new() { Allowed = false, Message = message };
    }

    public class SubscriptionLimitService
    {
        private readonly ApplicationDbContext _context;

        public SubscriptionLimitService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<CompanyUsageViewModel> GetUsageAsync(int companyId)
        {
            var company = await _context.PropertyManagementCompanies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyId == companyId);

            if (company == null)
            {
                return new CompanyUsageViewModel("Unknown", 0, 0, 0, 0, 0, 0);
            }

            var propertyCount = await _context.Properties.CountAsync(p => p.CompanyId == companyId);
            var unitCount = await _context.Units.CountAsync(u => u.Property.CompanyId == companyId);
            var managerCount = await CountManagersAsync(companyId);

            return new CompanyUsageViewModel(
                company.SubscriptionPlan,
                propertyCount,
                company.MaxProperties,
                unitCount,
                company.MaxUnits,
                managerCount,
                company.MaxManagers);
        }

        public async Task<SubscriptionLimitResult> CanAddPropertyAsync(int companyId)
        {
            var company = await GetCompanyAsync(companyId);
            if (company == null)
            {
                return SubscriptionLimitResult.Denied("Company not found.");
            }

            if (!IsSubscriptionUsable(company.SubscriptionStatus))
            {
                return SubscriptionLimitResult.Denied("Your subscription is not active. Contact your platform administrator or complete billing setup.");
            }

            var currentCount = await _context.Properties.CountAsync(p => p.CompanyId == companyId);
            if (currentCount >= company.MaxProperties)
            {
                return SubscriptionLimitResult.Denied(
                    $"Property limit reached ({company.MaxProperties} on {company.SubscriptionPlan} plan). Upgrade your subscription to add more properties.");
            }

            return SubscriptionLimitResult.Success();
        }

        public Task<SubscriptionLimitResult> CanAddUnitAsync(int companyId) =>
            CanAddUnitsAsync(companyId, 1);

        public async Task<SubscriptionLimitResult> CanAddUnitsAsync(int companyId, int unitsToAdd)
        {
            if (unitsToAdd < 0)
            {
                return SubscriptionLimitResult.Denied("Unit count cannot be negative.");
            }

            if (unitsToAdd == 0)
            {
                return SubscriptionLimitResult.Success();
            }

            var company = await GetCompanyAsync(companyId);
            if (company == null)
            {
                return SubscriptionLimitResult.Denied("Company not found.");
            }

            if (!IsSubscriptionUsable(company.SubscriptionStatus))
            {
                return SubscriptionLimitResult.Denied("Your subscription is not active. Contact your platform administrator or complete billing setup.");
            }

            var currentCount = await _context.Units.CountAsync(u => u.Property.CompanyId == companyId);
            var remaining = Math.Max(0, company.MaxUnits - currentCount);

            if (currentCount + unitsToAdd > company.MaxUnits)
            {
                return SubscriptionLimitResult.Denied(
                    $"Cannot add {unitsToAdd} units. You have {currentCount}/{company.MaxUnits} used; only {remaining} remaining on your {company.SubscriptionPlan} plan.");
            }

            return SubscriptionLimitResult.Success();
        }

        public async Task<SubscriptionLimitResult> CanAddManagerAsync(int companyId)
        {
            var company = await GetCompanyAsync(companyId);
            if (company == null)
            {
                return SubscriptionLimitResult.Denied("Company not found.");
            }

            if (!IsSubscriptionUsable(company.SubscriptionStatus))
            {
                return SubscriptionLimitResult.Denied("Your subscription is not active. Contact your platform administrator or complete billing setup.");
            }

            if (SubscriptionPlans.IsUnlimitedManagers(company.MaxManagers))
            {
                return SubscriptionLimitResult.Success();
            }

            var currentCount = await CountManagersAsync(companyId);
            if (currentCount >= company.MaxManagers)
            {
                return SubscriptionLimitResult.Denied(
                    $"Manager account limit reached ({company.MaxManagers} on {company.SubscriptionPlan} plan). Upgrade your subscription to add more managers.");
            }

            return SubscriptionLimitResult.Success();
        }

        public async Task<SubscriptionLimitResult> CanAddAdministratorAsync(int companyId, string? excludeUserId = null)
        {
            var company = await GetCompanyAsync(companyId);
            if (company == null)
            {
                return SubscriptionLimitResult.Denied("Company not found.");
            }

            var currentCount = await CountAdministratorsAsync(companyId, excludeUserId);
            if (currentCount >= SubscriptionPlans.MaxAdministratorsPerCompany)
            {
                return SubscriptionLimitResult.Denied(
                    "Each company is allowed one administrator account. Manager accounts are limited by your subscription plan.");
            }

            return SubscriptionLimitResult.Success();
        }

        public Task<int> CountAdministratorsAsync(int companyId, string? excludeUserId = null) =>
            CountUsersInRoleForCompanyAsync(companyId, "Administrator", excludeUserId);

        private async Task<PropertyManagementCompany?> GetCompanyAsync(int companyId) =>
            await _context.PropertyManagementCompanies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyId == companyId);

        private async Task<int> CountManagersAsync(int companyId)
        {
            var managerRoleId = await _context.Roles
                .Where(r => r.Name == "Manager")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            if (managerRoleId == null)
            {
                return 0;
            }

            return await _context.UserRoles
                .Where(ur => ur.RoleId == managerRoleId)
                .Join(_context.Users.Where(u => u.CompanyId == companyId), ur => ur.UserId, u => u.Id, (_, u) => u)
                .CountAsync();
        }

        private async Task<int> CountUsersInRoleForCompanyAsync(int companyId, string roleName, string? excludeUserId)
        {
            var roleId = await _context.Roles
                .Where(r => r.Name == roleName)
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            if (roleId == null)
            {
                return 0;
            }

            var query = _context.UserRoles
                .Where(ur => ur.RoleId == roleId)
                .Join(_context.Users.Where(u => u.CompanyId == companyId), ur => ur.UserId, u => u.Id, (_, u) => u);

            if (!string.IsNullOrWhiteSpace(excludeUserId))
            {
                query = query.Where(u => u.Id != excludeUserId);
            }

            return await query.CountAsync();
        }

        private static bool IsSubscriptionUsable(string status) =>
            status is CompanySubscriptionStatuses.Active or CompanySubscriptionStatuses.Trialing;
    }
}
