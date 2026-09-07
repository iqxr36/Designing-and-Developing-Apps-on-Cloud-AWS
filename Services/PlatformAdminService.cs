using CloudMVCApplication.Data;
using CloudMVCApplication.Models;
using CloudMVCApplication.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CloudMVCApplication.Services
{
    public class PlatformAdminService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly CompanyBillingHistoryService _billingHistory;
        private readonly TechnicianPayoutService _payoutService;
        private readonly IFileStorageService _fileStorage;

        public PlatformAdminService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            CompanyBillingHistoryService billingHistory,
            TechnicianPayoutService payoutService,
            IFileStorageService fileStorage)
        {
            _context = context;
            _userManager = userManager;
            _billingHistory = billingHistory;
            _payoutService = payoutService;
            _fileStorage = fileStorage;
        }

        public async Task<SuperAdminDashboardViewModel> GetDashboardAsync()
        {
            var companies = await _context.PropertyManagementCompanies.AsNoTracking().ToListAsync();
            var pendingTechnicians = await GetPendingTechnicianCountAsync();
            var pendingBankTransfers = await _payoutService.GetPendingTransferCountAsync();
            var pastDueCount = companies.Count(c => c.SubscriptionStatus == CompanySubscriptionStatuses.PastDue);

            var paidPayments = await _context.TechnicianPayments
                .AsNoTracking()
                .Where(p => p.PaymentStatus == TechnicianPaymentStatuses.Paid)
                .ToListAsync();

            var totalPlatformFeesEarned = paidPayments.Sum(p => p.PlatformCommissionAmount);
            var pendingPayoutPaymentIds = await _context.TechnicianPayouts
                .AsNoTracking()
                .Where(p => p.PayoutStatus == TechnicianPayoutStatuses.Pending)
                .Select(p => p.TechnicianPaymentId)
                .ToListAsync();
            var pendingPlatformFees = paidPayments
                .Where(p => pendingPayoutPaymentIds.Contains(p.Id))
                .Sum(p => p.PlatformCommissionAmount);

            var paidSubscriptionRevenue = await _context.SubscriptionPayments
                .AsNoTracking()
                .Where(p => p.PaymentStatus == PaymentStatuses.Paid)
                .Select(p => (decimal?)p.GrossAmount)
                .SumAsync() ?? 0m;

            var pendingSubscriptionRevenue = await _context.SubscriptionPayments
                .AsNoTracking()
                .Where(p => p.PaymentStatus == PaymentStatuses.Pending)
                .Select(p => (decimal?)p.GrossAmount)
                .SumAsync() ?? 0m;

            totalPlatformFeesEarned += paidSubscriptionRevenue;
            pendingPlatformFees += pendingSubscriptionRevenue;

            return new SuperAdminDashboardViewModel
            {
                TotalCompanies = companies.Count,
                ActiveCompanies = companies.Count(c => c.Status == "Active"),
                SuspendedCompanies = companies.Count(c => c.Status == "Suspended"),
                PendingTechnicians = pendingTechnicians,
                PendingBankTransfers = pendingBankTransfers,
                TotalPlatformFeesEarned = totalPlatformFeesEarned,
                PendingPlatformFees = pendingPlatformFees,
                PastDueSubscriptions = pastDueCount,
                PlanBreakdown = SubscriptionPlans.All
                    .Select(plan => new SuperAdminPlanCountViewModel
                    {
                        Plan = plan,
                        Count = companies.Count(c => c.SubscriptionPlan == plan)
                    })
                    .ToList(),
                RecentCompanies = companies
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(5)
                    .Select(MapCompanySummary)
                    .ToList()
            };
        }

        public async Task<List<SuperAdminCompanyListItemViewModel>> GetCompaniesAsync(string? search, string? plan, string? status, bool includeDeleted = false)
        {
            var query = _context.PropertyManagementCompanies.AsNoTracking().AsQueryable();

            if (!includeDeleted)
            {
                query = query.Where(c => c.Status != "Deleted");
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c =>
                    c.CompanyName.Contains(search) ||
                    c.CompanyEmail.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(plan))
            {
                query = query.Where(c => c.SubscriptionPlan == plan);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(c => c.SubscriptionStatus == status || c.Status == status);
            }

            var companies = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
            var companyIds = companies.Select(c => c.CompanyId).ToList();

            var propertyCounts = await _context.Properties
                .Where(p => companyIds.Contains(p.CompanyId))
                .GroupBy(p => p.CompanyId)
                .Select(g => new { CompanyId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CompanyId, x => x.Count);

            var unitCounts = await _context.Units
                .Where(u => companyIds.Contains(u.Property.CompanyId))
                .GroupBy(u => u.Property.CompanyId)
                .Select(g => new { CompanyId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CompanyId, x => x.Count);

            return companies.Select(c =>
            {
                var summary = MapCompanySummary(c);
                summary.PropertyCount = propertyCounts.GetValueOrDefault(c.CompanyId);
                summary.UnitCount = unitCounts.GetValueOrDefault(c.CompanyId);
                return summary;
            }).ToList();
        }

        public async Task<SuperAdminCompanyDetailViewModel?> GetCompanyDetailAsync(int companyId)
        {
            var company = await _context.PropertyManagementCompanies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyId == companyId);

            if (company == null)
            {
                return null;
            }

            var propertyCount = await _context.Properties.CountAsync(p => p.CompanyId == companyId);
            var unitCount = await _context.Units.CountAsync(u => u.Property.CompanyId == companyId);
            var adminRoleId = await _context.Roles.Where(r => r.Name == "Administrator").Select(r => r.Id).FirstOrDefaultAsync();
            var managerRoleId = await _context.Roles.Where(r => r.Name == "Manager").Select(r => r.Id).FirstOrDefaultAsync();

            var adminCount = adminRoleId == null
                ? 0
                : await _context.UserRoles
                    .Where(ur => ur.RoleId == adminRoleId)
                    .Join(_context.Users.Where(u => u.CompanyId == companyId), ur => ur.UserId, u => u.Id, (_, u) => u)
                    .CountAsync();

            var managerCount = managerRoleId == null
                ? 0
                : await _context.UserRoles
                    .Where(ur => ur.RoleId == managerRoleId)
                    .Join(_context.Users.Where(u => u.CompanyId == companyId), ur => ur.UserId, u => u.Id, (_, u) => u)
                    .CountAsync();

            var administrators = await GetCompanyAdminsAsync(companyId);
            var billingEvents = await _billingHistory.GetRecentEventsAsync(companyId);
            var invoices = await _billingHistory.GetInvoicesAsync(companyId);

            return new SuperAdminCompanyDetailViewModel
            {
                CompanyId = company.CompanyId,
                CompanyName = company.CompanyName,
                CompanyEmail = company.CompanyEmail,
                CompanyPhone = company.CompanyPhone,
                CompanyAddress = company.CompanyAddress,
                Status = company.Status,
                SubscriptionPlan = company.SubscriptionPlan,
                SubscriptionStatus = company.SubscriptionStatus,
                ProviderCustomerId = company.XenditCustomerId ?? company.LegacyCustomerId,
                ProviderSubscriptionId = company.XenditSubscriptionId ?? company.LegacySubscriptionId,
                MaxProperties = company.MaxProperties,
                MaxUnits = company.MaxUnits,
                MaxManagers = company.MaxManagers,
                PropertyCount = propertyCount,
                UnitCount = unitCount,
                AdministratorCount = adminCount,
                ManagerCount = managerCount,
                CurrentPeriodStart = company.CurrentPeriodStart,
                CurrentPeriodEnd = company.CurrentPeriodEnd,
                TrialEndsAt = company.TrialEndsAt,
                CreatedAt = company.CreatedAt,
                Administrators = administrators,
                BillingEvents = billingEvents.Select(e => new SuperAdminBillingEventViewModel
                {
                    EventType = e.EventType,
                    PreviousPlan = e.PreviousPlan,
                    NewPlan = e.NewPlan,
                    PreviousStatus = e.PreviousStatus,
                    NewStatus = e.NewStatus,
                    Amount = e.Amount,
                    Currency = e.Currency,
                    CreatedAt = e.CreatedAt
                }).ToList(),
                Invoices = invoices.Select(i => new SuperAdminInvoiceViewModel
                {
                    ProviderInvoiceId = i.ProviderInvoiceId,
                    AmountDue = i.AmountDue,
                    AmountPaid = i.AmountPaid,
                    Currency = i.Currency,
                    Status = i.Status,
                    PeriodStart = i.PeriodStart,
                    PeriodEnd = i.PeriodEnd,
                    HostedInvoiceUrl = i.HostedInvoiceUrl,
                    CreatedAt = i.CreatedAt
                }).ToList()
            };
        }

        public async Task<bool> UpdateSubscriptionStatusAsync(int companyId, string status, DateTime? trialEndsAt)
        {
            if (!CompanySubscriptionStatuses.All.Contains(status))
            {
                return false;
            }

            var company = await _context.PropertyManagementCompanies.FirstOrDefaultAsync(c => c.CompanyId == companyId);
            if (company == null)
            {
                return false;
            }

            var previousStatus = company.SubscriptionStatus;
            company.SubscriptionStatus = status;
            company.TrialEndsAt = trialEndsAt;

            if (status == CompanySubscriptionStatuses.Suspended || status == CompanySubscriptionStatuses.Cancelled)
            {
                company.Status = "Suspended";
            }
            else if (status is CompanySubscriptionStatuses.Active or CompanySubscriptionStatuses.Trialing)
            {
                company.Status = "Active";
            }

            await _context.SaveChangesAsync();

            if (!string.Equals(previousStatus, status, StringComparison.Ordinal))
            {
                await _billingHistory.RecordEventAsync(
                    companyId,
                    CompanySubscriptionEventTypes.StatusChanged,
                    previousStatus: previousStatus,
                    newStatus: status);
            }

            return true;
        }

        public async Task<List<SuperAdminCompanyAdminViewModel>> GetCompanyAdminsAsync(int? companyId = null)
        {
            var adminRoleId = await _context.Roles
                .Where(r => r.Name == "Administrator")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            if (adminRoleId == null)
            {
                return [];
            }

            var query = _context.UserRoles
                .Where(ur => ur.RoleId == adminRoleId)
                .Join(_context.Users, ur => ur.UserId, u => u.Id, (_, u) => u)
                .AsQueryable();

            if (companyId.HasValue)
            {
                query = query.Where(u => u.CompanyId == companyId.Value);
            }

            var users = await query
                .Include(u => u.Company)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            return users.Select(u => new SuperAdminCompanyAdminViewModel
            {
                UserId = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                CompanyId = u.CompanyId,
                CompanyName = u.Company?.CompanyName ?? "—",
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt
            }).ToList();
        }

        public async Task<List<SuperAdminTechnicianViewModel>> GetTechniciansAsync(
            bool pendingOnly = false,
            string? reviewStatus = null)
        {
            var techRoleId = await _context.Roles
                .Where(r => r.Name == "Technician")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            if (techRoleId == null)
            {
                return [];
            }

            var query = _context.UserRoles
                .Where(ur => ur.RoleId == techRoleId)
                .Join(_context.Users, ur => ur.UserId, u => u.Id, (_, u) => u)
                .Include(u => u.TechnicianProfile)
                    .ThenInclude(p => p!.UploadedDocuments)
                .AsQueryable();

            if (pendingOnly)
            {
                query = query.Where(u =>
                    u.TechnicianProfile != null &&
                    u.TechnicianProfile.AvailabilityStatus == "Pending Review");
            }
            else
            {
                query = query.Where(u =>
                    u.TechnicianProfile != null &&
                    u.TechnicianProfile.AvailabilityStatus != "Pending Review");

                if (string.Equals(reviewStatus, "approved", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(u => u.TechnicianProfile!.AvailabilityStatus == "Available");
                }
                else if (string.Equals(reviewStatus, "rejected", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(u => u.TechnicianProfile!.AvailabilityStatus == "Rejected");
                }
            }

            var users = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();

            return users.Select(u => new SuperAdminTechnicianViewModel
            {
                UserId = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                PhoneNumber = u.PhoneNumber,
                IsActive = u.IsActive,
                Specialization = u.TechnicianProfile?.Specialization ?? "—",
                AvailabilityStatus = u.TechnicianProfile?.AvailabilityStatus ?? "Unknown",
                ExperienceYears = u.TechnicianProfile?.ExperienceYears ?? 0,
                ServiceArea = u.TechnicianProfile?.ServiceArea,
                CreatedAt = u.CreatedAt,
                IsPending = u.TechnicianProfile?.AvailabilityStatus == "Pending Review",
                PayoutBankSummary = u.TechnicianProfile == null ||
                    string.IsNullOrWhiteSpace(u.TechnicianProfile.PayoutBankName)
                    ? "Not set"
                    : $"{u.TechnicianProfile.PayoutBankName} {TechnicianPayoutService.MaskAccountNumber(u.TechnicianProfile.PayoutAccountNumber)}",
                Documents = u.TechnicianProfile?.UploadedDocuments
                    .OrderByDescending(d => d.UploadedAt)
                    .Select(d => new SuperAdminTechnicianDocumentViewModel
                    {
                        Id = d.Id,
                        FileName = d.FileName,
                        FilePath = d.FilePath,
                        FileType = d.FileType,
                        UploadedAt = d.UploadedAt
                    })
                    .ToList() ?? []
            }).ToList();
        }

        public async Task<SuperAdminTechnicianViewModel?> GetTechnicianDetailAsync(string userId)
        {
            var techRoleId = await _context.Roles
                .Where(r => r.Name == "Technician")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            if (techRoleId == null)
            {
                return null;
            }

            var user = await _context.UserRoles
                .Where(ur => ur.RoleId == techRoleId && ur.UserId == userId)
                .Join(_context.Users, ur => ur.UserId, u => u.Id, (_, u) => u)
                .Include(u => u.TechnicianProfile)
                    .ThenInclude(p => p!.UploadedDocuments)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return null;
            }

            return new SuperAdminTechnicianViewModel
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                IsActive = user.IsActive,
                Specialization = user.TechnicianProfile?.Specialization ?? "—",
                AvailabilityStatus = user.TechnicianProfile?.AvailabilityStatus ?? "Unknown",
                ExperienceYears = user.TechnicianProfile?.ExperienceYears ?? 0,
                ServiceArea = user.TechnicianProfile?.ServiceArea,
                CreatedAt = user.CreatedAt,
                IsPending = user.TechnicianProfile?.AvailabilityStatus == "Pending Review",
                PayoutBankSummary = user.TechnicianProfile == null ||
                    string.IsNullOrWhiteSpace(user.TechnicianProfile.PayoutBankName)
                    ? "Not set"
                    : $"{user.TechnicianProfile.PayoutBankName} {TechnicianPayoutService.MaskAccountNumber(user.TechnicianProfile.PayoutAccountNumber)}",
                Documents = user.TechnicianProfile?.UploadedDocuments
                    .OrderByDescending(d => d.UploadedAt)
                    .Select(d => new SuperAdminTechnicianDocumentViewModel
                    {
                        Id = d.Id,
                        FileName = d.FileName,
                        FilePath = d.FilePath,
                        FileType = d.FileType,
                        UploadedAt = d.UploadedAt
                    })
                    .ToList() ?? []
            };
        }

        public async Task<List<SuperAdminPendingPayoutViewModel>> GetPendingPayoutsAsync()
        {
            var payments = await _payoutService.GetPendingTransfersAsync();

            return payments.Select(p => new SuperAdminPendingPayoutViewModel
            {
                PaymentId = p.Id,
                RequestId = p.TechnicianPayment.MaintenanceRequestId,
                DisplayRequestId = $"REQ-{p.TechnicianPayment.MaintenanceRequestId:0000}",
                CompanyName = p.TechnicianPayment.MaintenanceRequest.Property.Company.CompanyName,
                TechnicianName = p.Technician.User.FullName,
                TechnicianEmail = p.Technician.User.Email ?? string.Empty,
                CompanyChargeAmount = p.TechnicianPayment.GrossAmount,
                PlatformFeeAmount = p.TechnicianPayment.PlatformCommissionAmount,
                TechnicianTransferAmount = p.Amount,
                CompanyChargedAt = p.TechnicianPayment.PaidAt,
                AccountHolderName = p.Technician.PayoutAccountHolderName ?? string.Empty,
                BankName = p.Technician.PayoutBankName ?? string.Empty,
                AccountNumber = p.Technician.PayoutAccountNumber ?? string.Empty
            }).ToList();
        }

        public async Task<bool> SetUserActiveAsync(string userId, bool isActive)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            user.IsActive = isActive;
            await _userManager.UpdateAsync(user);
            return true;
        }

        public async Task<bool> ApproveTechnicianAsync(string userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return false;
            }

            var profile = await _context.TechnicianProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

            user.IsActive = true;
            if (profile != null)
            {
                profile.AvailabilityStatus = "Available";
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RejectTechnicianAsync(string userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return false;
            }

            var profile = await _context.TechnicianProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

            user.IsActive = false;
            if (profile != null)
            {
                profile.AvailabilityStatus = "Rejected";
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<(bool Succeeded, int CompanyId, IEnumerable<string> Errors)> CreateCompanyAsync(
            SuperAdminCreateCompanyViewModel input)
        {
            var errors = new List<string>();
            var plan = SubscriptionPlans.Normalize(input.Plan);

            if (await _context.PropertyManagementCompanies.AnyAsync(c => c.CompanyEmail == input.CompanyEmail))
            {
                errors.Add("A company with this business email already exists.");
                return (false, 0, errors);
            }

            if (await _userManager.FindByEmailAsync(input.CompanyEmail) != null)
            {
                errors.Add("An account with this email already exists.");
                return (false, 0, errors);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var (maxProperties, maxUnits, maxManagers) = SubscriptionPlans.GetLimits(plan);
                var company = new PropertyManagementCompany
                {
                    CompanyName = input.CompanyName.Trim(),
                    CompanyEmail = input.CompanyEmail.Trim(),
                    CompanyPhone = input.CompanyPhone?.Trim(),
                    CompanyAddress = input.CompanyAddress?.Trim(),
                    Status = "Active",
                    SubscriptionPlan = plan,
                    MaxProperties = maxProperties,
                    MaxUnits = maxUnits,
                    MaxManagers = maxManagers,
                    SubscriptionStatus = CompanySubscriptionStatuses.Trialing,
                    TrialEndsAt = DateTime.UtcNow.AddDays(14),
                    CreatedAt = DateTime.UtcNow
                };

                _context.PropertyManagementCompanies.Add(company);
                await _context.SaveChangesAsync();

                var adminUser = new ApplicationUser
                {
                    UserName = input.CompanyEmail.Trim(),
                    Email = input.CompanyEmail.Trim(),
                    PhoneNumber = input.AdminPhone.Trim(),
                    FullName = input.AdminName.Trim(),
                    CompanyId = company.CompanyId,
                    IsActive = true,
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createUserResult = await _userManager.CreateAsync(adminUser, input.Password);
                if (!createUserResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return (false, 0, createUserResult.Errors.Select(e => e.Description));
                }

                if (!await _context.Roles.AnyAsync(r => r.Name == "Administrator"))
                {
                    await transaction.RollbackAsync();
                    return (false, 0, ["Administrator role is missing."]);
                }

                var roleResult = await _userManager.AddToRoleAsync(adminUser, "Administrator");
                if (!roleResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return (false, 0, roleResult.Errors.Select(e => e.Description));
                }

                _context.AdministratorProfiles.Add(new AdministratorProfile
                {
                    UserId = adminUser.Id,
                    Department = "Operations",
                    AccessLevel = "Company Administrator",
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return (true, company.CompanyId, Array.Empty<string>());
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<(bool Succeeded, IEnumerable<string> Errors)> UpdateCompanyAsync(
            SuperAdminEditCompanyViewModel input)
        {
            var company = await _context.PropertyManagementCompanies
                .FirstOrDefaultAsync(c => c.CompanyId == input.CompanyId);

            if (company == null)
            {
                return (false, ["Company not found."]);
            }

            var emailTaken = await _context.PropertyManagementCompanies
                .AnyAsync(c => c.CompanyId != input.CompanyId && c.CompanyEmail == input.CompanyEmail);
            if (emailTaken)
            {
                return (false, ["A company with this business email already exists."]);
            }

            company.CompanyName = input.CompanyName.Trim();
            company.CompanyEmail = input.CompanyEmail.Trim();
            company.CompanyPhone = input.CompanyPhone?.Trim();
            company.CompanyAddress = input.CompanyAddress?.Trim();
            await _context.SaveChangesAsync();
            return (true, Array.Empty<string>());
        }

        public async Task<bool> SoftDeleteCompanyAsync(int companyId)
        {
            var company = await _context.PropertyManagementCompanies
                .FirstOrDefaultAsync(c => c.CompanyId == companyId);
            if (company == null)
            {
                return false;
            }

            company.Status = "Deleted";
            company.SubscriptionStatus = CompanySubscriptionStatuses.Suspended;

            var users = await _context.Users.Where(u => u.CompanyId == companyId).ToListAsync();
            foreach (var user in users)
            {
                user.IsActive = false;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<(bool Succeeded, string? UserId, IEnumerable<string> Errors)> CreateTechnicianAsync(
            SuperAdminCreateTechnicianViewModel input,
            IFileStorageService fileStorage,
            CancellationToken cancellationToken = default)
        {
            if (await _userManager.FindByEmailAsync(input.Email) != null)
            {
                return (false, null, ["An account with this email already exists."]);
            }

            if (!await _context.Roles.AnyAsync(r => r.Name == "Technician"))
            {
                return (false, null, ["Technician role is missing."]);
            }

            var user = new ApplicationUser
            {
                UserName = input.Email.Trim(),
                Email = input.Email.Trim(),
                PhoneNumber = input.PhoneNumber.Trim(),
                FullName = input.FullName.Trim(),
                CompanyId = null,
                IsActive = true,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user, input.Password);
            if (!createResult.Succeeded)
            {
                return (false, null, createResult.Errors.Select(e => e.Description));
            }

            var roleResult = await _userManager.AddToRoleAsync(user, "Technician");
            if (!roleResult.Succeeded)
            {
                return (false, null, roleResult.Errors.Select(e => e.Description));
            }

            var profile = new TechnicianProfile
            {
                UserId = user.Id,
                Specialization = input.Specialization.Trim(),
                ExperienceYears = input.ExperienceYears,
                ServiceArea = input.ServiceArea.Trim(),
                AvailabilityStatus = "Available",
                AverageRating = 0,
                TotalCompletedJobs = 0,
                CreatedAt = DateTime.UtcNow
            };
            _context.TechnicianProfiles.Add(profile);
            await _context.SaveChangesAsync(cancellationToken);

            foreach (var file in input.Certificates.Where(f => f.Length > 0))
            {
                var originalFileName = Path.GetFileName(file.FileName);
                var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
                var storedFileName = $"{Guid.NewGuid():N}{extension}";
                var filePath = await fileStorage.UploadAsync(
                    file,
                    $"technician-documents/{user.Id}",
                    storedFileName,
                    cancellationToken);

                _context.TechnicianDocuments.Add(new TechnicianDocument
                {
                    TechnicianId = profile.TechnicianId,
                    FileName = originalFileName,
                    FilePath = filePath,
                    FileType = string.IsNullOrWhiteSpace(file.ContentType)
                        ? extension.TrimStart('.')
                        : file.ContentType,
                    UploadedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            return (true, user.Id, Array.Empty<string>());
        }

        public async Task<(bool Succeeded, IEnumerable<string> Errors)> UpdateTechnicianAsync(
            SuperAdminEditTechnicianViewModel input)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == input.UserId);
            if (user == null)
            {
                return (false, ["Technician not found."]);
            }

            var emailOwner = await _userManager.FindByEmailAsync(input.Email);
            if (emailOwner != null && emailOwner.Id != user.Id)
            {
                return (false, ["An account with this email already exists."]);
            }

            var profile = await _context.TechnicianProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile == null)
            {
                return (false, ["Technician profile not found."]);
            }

            user.FullName = input.FullName.Trim();
            user.Email = input.Email.Trim();
            user.UserName = input.Email.Trim();
            user.PhoneNumber = input.PhoneNumber?.Trim();
            user.IsActive = input.IsActive;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return (false, updateResult.Errors.Select(e => e.Description));
            }

            profile.Specialization = input.Specialization.Trim();
            profile.ExperienceYears = input.ExperienceYears;
            profile.ServiceArea = input.ServiceArea.Trim();
            profile.AvailabilityStatus = string.IsNullOrWhiteSpace(input.AvailabilityStatus)
                ? profile.AvailabilityStatus
                : input.AvailabilityStatus.Trim();

            await _context.SaveChangesAsync();
            return (true, Array.Empty<string>());
        }

        public async Task<(bool Success, string? Error)> HardDeleteTechnicianAsync(string userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return (false, "Technician not found.");
            }

            var profile = await _context.TechnicianProfiles
                .Include(p => p.UploadedDocuments)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile != null)
            {
                var technicianId = profile.TechnicianId;
                var hasHistory =
                    await _context.Assignments.AnyAsync(a => a.TechnicianId == technicianId) ||
                    await _context.TechnicianPayments.AnyAsync(p => p.TechnicianId == technicianId) ||
                    await _context.TechnicianPayouts.AnyAsync(p => p.TechnicianId == technicianId) ||
                    await _context.ServicePayments.AnyAsync(p => p.TechnicianId == technicianId) ||
                    await _context.RequestFeedbacks.AnyAsync(f => f.TechnicianId == technicianId);

                if (hasHistory)
                {
                    return (false,
                        "Technician has job/payment history and cannot be permanently deleted. Use Deactivate instead.");
                }
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (profile != null)
                {
                    foreach (var document in profile.UploadedDocuments.ToList())
                    {
                        if (!string.IsNullOrWhiteSpace(document.FilePath) &&
                            document.FilePath.StartsWith(S3FileStorageService.StorageRoutePrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                await _fileStorage.DeleteAsync(document.FilePath);
                            }
                            catch
                            {
                                // Continue removing DB rows even if an S3 object is already gone.
                            }
                        }
                    }

                    _context.TechnicianDocuments.RemoveRange(profile.UploadedDocuments);
                    _context.TechnicianProfiles.Remove(profile);
                    await _context.SaveChangesAsync();
                }

                var notifications = await _context.Notifications
                    .Where(n => n.UserId == userId)
                    .ToListAsync();
                _context.Notifications.RemoveRange(notifications);

                var participants = await _context.ConversationParticipants
                    .Where(p => p.UserId == userId)
                    .ToListAsync();
                _context.ConversationParticipants.RemoveRange(participants);

                var messages = await _context.Messages
                    .Where(m => m.SenderUserId == userId)
                    .ToListAsync();
                _context.Messages.RemoveRange(messages);

                var statusHistory = await _context.RequestStatusHistories
                    .Where(h => h.ChangedByUserId == userId)
                    .ToListAsync();
                _context.RequestStatusHistories.RemoveRange(statusHistory);

                var requestImages = await _context.RequestImages
                    .Where(i => i.UploadedByUserId == userId)
                    .ToListAsync();
                _context.RequestImages.RemoveRange(requestImages);

                var auditLogs = await _context.AuditLogs
                    .Where(a => a.UserId == userId)
                    .ToListAsync();
                _context.AuditLogs.RemoveRange(auditLogs);

                await _context.SaveChangesAsync();

                var deleteResult = await _userManager.DeleteAsync(user);
                if (!deleteResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    var error = string.Join(" ", deleteResult.Errors.Select(e => e.Description));
                    return (false, string.IsNullOrWhiteSpace(error) ? "Could not delete technician account." : error);
                }

                await transaction.CommitAsync();
                return (true, null);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task AddAuditLogAsync(string userId, string action, string entityName, string entityId, string description)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                Description = description,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        public async Task<List<AuditLogRowViewModel>> GetPlatformAuditLogsAsync(string? userSearch, string? actionSearch)
        {
            var superAdminRoleId = await _context.Roles
                .Where(r => r.Name == "SuperAdmin")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            if (superAdminRoleId == null)
            {
                return [];
            }

            var superAdminUserIds = _context.UserRoles
                .Where(ur => ur.RoleId == superAdminRoleId)
                .Select(ur => ur.UserId);

            var query = _context.AuditLogs
                .Include(a => a.User)
                .Where(a => superAdminUserIds.Contains(a.UserId));

            if (!string.IsNullOrWhiteSpace(userSearch))
            {
                query = query.Where(l => l.User.FullName.Contains(userSearch));
            }

            if (!string.IsNullOrWhiteSpace(actionSearch))
            {
                query = query.Where(l => l.Action.Contains(actionSearch));
            }

            return await query
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new AuditLogRowViewModel(
                    l.AuditLogId,
                    l.User.FullName,
                    l.Action,
                    l.EntityName,
                    l.EntityId,
                    l.Description,
                    l.CreatedAt))
                .ToListAsync();
        }

        private async Task<int> GetPendingTechnicianCountAsync()
        {
            var techRoleId = await _context.Roles
                .Where(r => r.Name == "Technician")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            if (techRoleId == null)
            {
                return 0;
            }

            return await _context.UserRoles
                .Where(ur => ur.RoleId == techRoleId)
                .Join(_context.Users, ur => ur.UserId, u => u.Id, (_, u) => u)
                .Include(u => u.TechnicianProfile)
                .CountAsync(u =>
                    u.TechnicianProfile != null &&
                    u.TechnicianProfile.AvailabilityStatus == "Pending Review");
        }

        private static SuperAdminCompanyListItemViewModel MapCompanySummary(PropertyManagementCompany company) =>
            new()
            {
                CompanyId = company.CompanyId,
                CompanyName = company.CompanyName,
                CompanyEmail = company.CompanyEmail,
                Status = company.Status,
                SubscriptionPlan = company.SubscriptionPlan,
                SubscriptionStatus = company.SubscriptionStatus,
                CreatedAt = company.CreatedAt
            };
    }
}
