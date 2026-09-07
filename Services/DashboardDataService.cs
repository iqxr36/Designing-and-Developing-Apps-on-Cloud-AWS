using CloudMVCApplication.Data;
using CloudMVCApplication.Models;
using CloudMVCApplication.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CloudMVCApplication.Services
{
    public class DashboardDataService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly TechnicianWorkloadService _workloadService;

        public DashboardDataService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            TechnicianWorkloadService workloadService)
        {
            _context = context;
            _userManager = userManager;
            _workloadService = workloadService;
        }

        // Builds the administrator dashboard model scoped to the administrator's company.
        public async Task<AreaDashboardViewModel> GetAdministratorDashboardAsync(string? userId = null, bool includeMaintenanceData = false)
        {
            var dbUser = string.IsNullOrWhiteSpace(userId)
                ? null
                : await _context.Users
                    .Include(u => u.Company)
                    .FirstOrDefaultAsync(u => u.Id == userId);

            var companyId = dbUser?.CompanyId;
            var users = (await BuildUsersAsync(companyId))
                .Where(u => u.Role is not ("Administrator" or "Technician"))
                .Where(u => dbUser == null || u.Id != dbUser.Id)
                .ToList();

            var requests = includeMaintenanceData
                ? await BuildRequestsAsync(companyId: companyId)
                : Array.Empty<RequestRowViewModel>();
            var properties = await BuildPropertiesAsync(companyId);
            var units = await BuildUnitsAsync(companyId);
            var managers = await BuildManagersAsync(companyId);

            var currentUser = dbUser == null
                ? users.FirstOrDefault(u => u.Role == "Administrator")
                : new UserRowViewModel(
                    dbUser.Id,
                    dbUser.FullName,
                    dbUser.Email ?? string.Empty,
                    dbUser.PhoneNumber,
                    ResolvePrimaryRole(await _userManager.GetRolesAsync(dbUser)),
                    dbUser.IsActive ? "Active" : "Inactive",
                    Initials(dbUser.FullName));

            return new AreaDashboardViewModel
            {
                CurrentUserName = currentUser?.FullName ?? "Administrator",
                CurrentUserEmail = currentUser?.Email ?? string.Empty,
                CurrentUserPhoneNumber = currentUser?.PhoneNumber,
                CurrentUserInitials = currentUser?.Initials ?? "AD",
                CurrentUserAvatarUrl = await GetAvatarUrlAsync(currentUser?.Id),
                CurrentCompanyName = dbUser?.Company?.CompanyName,
                Metrics = new[]
                {
                    new MetricCardViewModel("Total Properties", properties.Count.ToString(), "Active portfolio", "apartment"),
                    new MetricCardViewModel("Occupied Units", units.Count(u => u.Status == "Occupied").ToString(), $"{units.Count} total units", "door_front"),
                    new MetricCardViewModel("Total Managers", managers.Count.ToString(), "Assigned to company", "supervisor_account"),
                    new MetricCardViewModel("Active Users", users.Count(u => u.Status == "Active").ToString(), "Across all roles", "group")
                },
                Users = users,
                Properties = properties,
                Units = units,
                Managers = managers,
                Requests = requests,
                Technicians = includeMaintenanceData
                    ? await BuildTechniciansAsync()
                    : Array.Empty<TechnicianRowViewModel>(),
                Notifications = await BuildNotificationsAsync(userId),
                AuditLogs = await BuildAuditLogsAsync(companyId)
            };
        }

        // Builds manager dashboard/detail data, including selected request photos and timeline when requested.
        public async Task<AreaDashboardViewModel> GetManagerDashboardAsync(string? userId = null, int? selectedRequestId = null)
        {
            var currentUser = string.IsNullOrWhiteSpace(userId)
                ? null
                : await _context.Users
                    .Include(u => u.Company)
                    .FirstOrDefaultAsync(u => u.Id == userId);
            var users = await BuildUsersAsync();
            var requests = await BuildRequestsAsync(companyId: currentUser?.CompanyId);
            var technicians = await BuildTechniciansAsync();
            var manager = currentUser == null
                ? users.FirstOrDefault(u => u.Role == "Manager")
                : new UserRowViewModel(
                    currentUser.Id,
                    currentUser.FullName,
                    currentUser.Email ?? string.Empty,
                    currentUser.PhoneNumber,
                    "Manager",
                    currentUser.IsActive ? "Active" : "Inactive",
                    Initials(currentUser.FullName));
            var selectedRequest = selectedRequestId.HasValue
                ? requests.FirstOrDefault(r => r.RequestId == selectedRequestId.Value)
                : null;

            return new AreaDashboardViewModel
            {
                CurrentUserName = manager?.FullName ?? "Manager",
                CurrentUserEmail = manager?.Email ?? string.Empty,
                CurrentUserPhoneNumber = manager?.PhoneNumber,
                CurrentUserInitials = manager?.Initials ?? "MG",
                CurrentUserAvatarUrl = currentUser?.AvatarUrl,
                CurrentCompanyName = currentUser?.Company?.CompanyName,
                Metrics = new[]
                {
                    new MetricCardViewModel("Open Requests", requests.Count(r => r.Status != "Completed").ToString(), "Needs action", "build"),
                    new MetricCardViewModel("Assigned Jobs", requests.Count(r => !string.IsNullOrWhiteSpace(r.TechnicianName)).ToString(), "With technicians", "assignment_ind"),
                    new MetricCardViewModel("Completed Repairs", requests.Count(r => r.Status == "Completed").ToString(), "Closed work", "task_alt"),
                    new MetricCardViewModel("Available Techs", technicians.Count(t => t.WorkloadPercentage < 100).ToString(), "Platform pool", "engineering")
                },
                Requests = requests,
                SelectedRequest = selectedRequest,
                SelectedRequestImages = selectedRequestId.HasValue
                    ? await BuildRequestImagesAsync(selectedRequestId.Value)
                    : Array.Empty<RequestImageRowViewModel>(),
                SelectedRequestStatusHistories = selectedRequestId.HasValue
                    ? await BuildRequestStatusHistoriesAsync(selectedRequestId.Value)
                    : Array.Empty<RequestStatusHistoryRowViewModel>(),
                Technicians = technicians,
                Notifications = await BuildNotificationsAsync(manager?.Id),
                AuditLogs = await BuildAuditLogsAsync(currentUser?.CompanyId)
            };
        }

        // Builds tenant dashboard/detail data for the signed-in tenant's unit and requests.
        public async Task<AreaDashboardViewModel> GetTenantDashboardAsync(string? userId = null, int? selectedRequestId = null)
        {
            var tenant = await _context.TenantProfiles
                .Include(t => t.User)
                    .ThenInclude(u => u.Company)
                .Include(t => t.Unit)
                    .ThenInclude(u => u.Property)
                .FirstOrDefaultAsync(t => userId == null || t.UserId == userId);

            var requests = await BuildRequestsAsync(tenant?.TenantId);
            var selectedRequest = selectedRequestId.HasValue
                ? requests.FirstOrDefault(r => r.RequestId == selectedRequestId.Value)
                : null;

            return new AreaDashboardViewModel
            {
                CurrentUserName = tenant?.User.FullName ?? "Tenant",
                CurrentUserEmail = tenant?.User.Email ?? string.Empty,
                CurrentUserPhoneNumber = tenant?.User.PhoneNumber,
                CurrentUserInitials = Initials(tenant?.User.FullName),
                CurrentUserAvatarUrl = tenant?.User.AvatarUrl,
                CurrentCompanyName = tenant?.User.Company?.CompanyName,
                Metrics = new[]
                {
                    new MetricCardViewModel("My Requests", requests.Count.ToString(), "All maintenance tickets", "receipt_long"),
                    new MetricCardViewModel("Open", requests.Count(r => r.Status != "Completed").ToString(), "In review or assigned", "pending_actions"),
                    new MetricCardViewModel("Completed", requests.Count(r => r.Status == "Completed").ToString(), "Resolved work", "check_circle"),
                    new MetricCardViewModel("Unit", tenant?.Unit.UnitNumber ?? "N/A", tenant?.Unit.Property.PropertyName, "home")
                },
                Units = tenant == null
                    ? Array.Empty<UnitRowViewModel>()
                    : new[]
                    {
                        new UnitRowViewModel(
                            tenant.UnitId,
                            tenant.Unit.UnitNumber,
                            tenant.Unit.Property.PropertyName,
                            tenant.Unit.UnitType,
                            tenant.Unit.Status,
                            tenant.User.FullName,
                            tenant.Unit.Property.Address,
                            tenant.Unit.Property.City)
                    },
                Requests = requests,
                SelectedRequest = selectedRequest,
                SelectedRequestImages = selectedRequestId.HasValue
                    ? await BuildRequestImagesAsync(selectedRequestId.Value)
                    : Array.Empty<RequestImageRowViewModel>(),
                SelectedRequestStatusHistories = selectedRequestId.HasValue
                    ? await BuildRequestStatusHistoriesAsync(selectedRequestId.Value)
                    : Array.Empty<RequestStatusHistoryRowViewModel>(),
                Notifications = await BuildNotificationsAsync(tenant?.UserId)
            };
        }

        // Builds technician dashboard data and calculates current workload/availability.
        public async Task<AreaDashboardViewModel> GetTechnicianDashboardAsync(string? userId = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new AreaDashboardViewModel
                {
                    CurrentUserName = "Technician",
                    CurrentUserInitials = "TC",
                    Metrics = new[]
                    {
                        new MetricCardViewModel("Assigned Jobs", "0", "Current workload", "assignment"),
                        new MetricCardViewModel("Completed Jobs", "0", "Lifetime total", "task_alt"),
                        new MetricCardViewModel("Rating", "0.0", "Tenant feedback", "star"),
                        new MetricCardViewModel("Availability", "Unavailable", null, "engineering")
                    }
                };
            }

            var technician = await _context.TechnicianProfiles
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.UserId == userId);

            var requests = technician == null
                ? Array.Empty<RequestRowViewModel>()
                : await BuildRequestsAsync(technicianId: technician.TechnicianId);

            var workload = technician == null
                ? _workloadService.CreateSnapshot(0)
                : _workloadService.CreateSnapshot(requests.Count(IsActiveRequest));

            return new AreaDashboardViewModel
            {
                CurrentUserName = technician?.User.FullName ?? "Technician",
                CurrentUserEmail = technician?.User.Email ?? string.Empty,
                CurrentUserPhoneNumber = technician?.User.PhoneNumber,
                CurrentUserInitials = Initials(technician?.User.FullName),
                CurrentUserAvatarUrl = technician?.User.AvatarUrl,
                Metrics = new[]
                {
                    new MetricCardViewModel("Active Jobs", $"{workload.ActiveJobs}/{workload.MaxActiveJobs}", "Current workload", "assignment"),
                    new MetricCardViewModel("Completed Jobs", (technician?.TotalCompletedJobs ?? 0).ToString(), "Lifetime total", "task_alt"),
                    new MetricCardViewModel("Rating", (technician?.AverageRating ?? 0).ToString("0.0"), "Tenant feedback", "star"),
                    new MetricCardViewModel("Availability", $"{workload.AvailabilityPercentage}%", workload.AvailabilityStatus, "engineering")
                },
                Requests = requests,
                Technicians = technician == null
                    ? Array.Empty<TechnicianRowViewModel>()
                    : new[]
                    {
                        new TechnicianRowViewModel(
                            technician.TechnicianId,
                            technician.User.FullName,
                            technician.User.Email ?? string.Empty,
                            technician.Specialization,
                            workload.AvailabilityStatus,
                            workload.ActiveJobs,
                            technician.TotalCompletedJobs,
                            technician.AverageRating,
                            workload.MaxActiveJobs,
                            workload.WorkloadPercentage,
                            workload.AvailabilityPercentage)
                    },
                Notifications = await BuildNotificationsAsync(technician?.UserId)
            };
        }

        // Projects application users into UI rows, optionally limited to one company.
        private async Task<IReadOnlyList<UserRowViewModel>> BuildUsersAsync(int? companyId = null)
        {
            var query = _context.Users.AsQueryable();
            if (companyId.HasValue)
            {
                query = query.Where(u => u.CompanyId == companyId.Value);
            }

            var users = await query
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var rows = new List<UserRowViewModel>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                rows.Add(new UserRowViewModel(
                    user.Id,
                    user.FullName,
                    user.Email ?? string.Empty,
                    user.PhoneNumber,
                    ResolvePrimaryRole(roles),
                    user.IsActive ? "Active" : "Inactive",
                    Initials(user.FullName)));
            }

            return rows;
        }

        private async Task<string?> GetAvatarUrlAsync(string? userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            return await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.AvatarUrl)
                .FirstOrDefaultAsync();
        }

        private async Task<IReadOnlyList<PropertyCardViewModel>> BuildPropertiesAsync(int? companyId = null)
        {
            var query = _context.Properties
                .Include(p => p.Units)
                .AsQueryable();

            if (companyId.HasValue)
            {
                query = query.Where(p => p.CompanyId == companyId.Value);
            }

            return await query
                .OrderBy(p => p.PropertyName)
                .Select(p => new PropertyCardViewModel(
                    p.PropertyId,
                    p.PropertyName,
                    p.Address ?? string.Empty,
                    p.City ?? string.Empty,
                    p.PropertyType,
                    p.Status,
                    p.ImageUrl,
                    p.Units.Count,
                    p.Units.Count(u => u.Status == "Occupied")))
                .ToListAsync();
        }

        private async Task<IReadOnlyList<UnitRowViewModel>> BuildUnitsAsync(int? companyId = null)
        {
            var query = _context.Units
                .Include(u => u.Property)
                .Include(u => u.TenantProfile)
                    .ThenInclude(t => t!.User)
                .AsQueryable();

            if (companyId.HasValue)
            {
                query = query.Where(u => u.Property.CompanyId == companyId.Value);
            }

            return await query
                .OrderBy(u => u.Property.PropertyName)
                .ThenBy(u => u.UnitNumber)
                .Select(u => new UnitRowViewModel(
                    u.UnitId,
                    u.UnitNumber,
                    u.Property.PropertyName,
                    u.UnitType,
                    u.Status,
                    u.TenantProfile == null ? null : u.TenantProfile.User.FullName,
                    u.Property.Address,
                    u.Property.City,
                    u.FloorNumber))
                .ToListAsync();
        }

        private async Task<IReadOnlyList<PropertyManagerRowViewModel>> BuildManagersAsync(int? companyId = null)
        {
            var query = _context.ManagerProfiles
                .Include(m => m.User)
                .Include(m => m.AssignedProperty)
                .AsQueryable();

            if (companyId.HasValue)
            {
                query = query.Where(m => m.User.CompanyId == companyId.Value);
            }

            return await query
                .OrderBy(m => m.User.FullName)
                .Select(m => new PropertyManagerRowViewModel(
                    m.ManagerId,
                    m.UserId,
                    m.User.FullName,
                    m.User.Email ?? string.Empty,
                    m.User.PhoneNumber,
                    m.JobTitle,
                    m.Region,
                    m.PropertyId,
                    m.AssignedProperty == null ? null : m.AssignedProperty.PropertyName))
                .ToListAsync();
        }

        // Projects maintenance requests into the shared row model used by dashboards and request lists.
        private async Task<IReadOnlyList<RequestRowViewModel>> BuildRequestsAsync(int? tenantId = null, int? technicianId = null, int? companyId = null)
        {
            var query = _context.MaintenanceRequests
                .Include(r => r.Tenant)
                    .ThenInclude(t => t.User)
                .Include(r => r.Unit)
                .Include(r => r.Property)
                .Include(r => r.Assignment)
                    .ThenInclude(a => a!.Technician)
                        .ThenInclude(t => t.User)
                .AsQueryable();

            if (tenantId.HasValue)
            {
                query = query.Where(r => r.TenantId == tenantId.Value);
            }

            if (technicianId.HasValue)
            {
                query = query.Where(r => r.Assignment != null && r.Assignment.TechnicianId == technicianId.Value);
            }

            if (companyId.HasValue)
            {
                query = query.Where(r => r.Property.CompanyId == companyId.Value);
            }

            return await query
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new RequestRowViewModel(
                    r.RequestId,
                    FormatRequestId(r.RequestId),
                    r.Title,
                    r.Description,
                    r.Category,
                    r.Priority,
                    r.Status,
                    r.CreatedAt,
                    r.Tenant.User.FullName,
                    r.Property.PropertyName,
                    r.Unit.UnitNumber,
                    r.Assignment == null ? null : r.Assignment.Technician.User.FullName,
                    r.Assignment == null ? null : r.Assignment.TechnicianId))
                .ToListAsync();
        }

        // Builds technician rows with workload snapshots so assignment screens can show capacity.
        private async Task<IReadOnlyList<TechnicianRowViewModel>> BuildTechniciansAsync()
        {
            var technicians = await _context.TechnicianProfiles
                .Include(t => t.User)
                .Include(t => t.Assignments)
                    .ThenInclude(a => a.Request)
                .Where(t => t.User.IsActive)
                .OrderBy(t => t.User.FullName)
                .ToListAsync();

            return technicians.Select(t =>
            {
                var workload = _workloadService.CreateSnapshot(t.Assignments.Count(_workloadService.IsActiveAssignment));
                return new TechnicianRowViewModel(
                    t.TechnicianId,
                    t.User.FullName,
                    t.User.Email ?? string.Empty,
                    t.Specialization,
                    workload.AvailabilityStatus,
                    workload.ActiveJobs,
                    t.TotalCompletedJobs,
                    t.AverageRating,
                    workload.MaxActiveJobs,
                    workload.WorkloadPercentage,
                    workload.AvailabilityPercentage);
            }).ToList();
        }

        // Loads the most recent notifications for a user or the latest platform-wide sample when no user is supplied.
        private async Task<IReadOnlyList<NotificationRowViewModel>> BuildNotificationsAsync(string? userId = null)
        {
            var query = _context.Notifications.AsQueryable();
            if (!string.IsNullOrWhiteSpace(userId))
            {
                query = query.Where(n => n.UserId == userId);
            }

            return await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(8)
                .Select(n => new NotificationRowViewModel(n.NotificationId, n.Title, n.Message, n.IsRead, n.CreatedAt, n.RequestId))
                .ToListAsync();
        }

        // Loads uploaded issue/proof images for request detail pages.
        private async Task<IReadOnlyList<RequestImageRowViewModel>> BuildRequestImagesAsync(int requestId)
        {
            return await _context.RequestImages
                .Include(i => i.UploadedByUser)
                .Where(i => i.RequestId == requestId)
                .OrderByDescending(i => i.UploadedAt)
                .Select(i => new RequestImageRowViewModel(
                    i.ImageId,
                    i.ImageUrl,
                    i.ImageType,
                    i.UploadedByUser.FullName,
                    i.UploadedAt))
                .ToListAsync();
        }

        // Loads timeline entries for the request detail timeline component.
        private async Task<IReadOnlyList<RequestStatusHistoryRowViewModel>> BuildRequestStatusHistoriesAsync(int requestId)
        {
            return await _context.RequestStatusHistories
                .Include(h => h.ChangedByUser)
                .Where(h => h.RequestId == requestId)
                .OrderByDescending(h => h.ChangedAt)
                .Select(h => new RequestStatusHistoryRowViewModel(
                    h.HistoryId,
                    h.OldStatus,
                    h.NewStatus,
                    h.Notes,
                    h.ChangedByUser.FullName,
                    h.ChangedAt))
                .ToListAsync();
        }

        // Loads recent audit log rows, optionally scoped to one company.
        private async Task<IReadOnlyList<AuditLogRowViewModel>> BuildAuditLogsAsync(int? companyId = null)
        {
            var query = _context.AuditLogs
                .Include(a => a.User)
                .AsQueryable();

            if (companyId.HasValue)
            {
                query = query.Where(a => a.User.CompanyId == companyId.Value);
            }

            return await query
                .OrderByDescending(a => a.CreatedAt)
                .Take(10)
                .Select(a => new AuditLogRowViewModel(
                    a.AuditLogId,
                    a.User.FullName,
                    a.Action,
                    a.EntityName,
                    a.EntityId,
                    a.Description,
                    a.CreatedAt))
                .ToListAsync();
        }

        private static string FormatRequestId(int requestId) => $"REQ-{requestId:0000}";

        private static bool IsActiveRequest(RequestRowViewModel request)
        {
            return request.Status is not ("Completed" or "Cancelled" or "Rejected");
        }

        private static string Initials(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return "NA";
            }

            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(parts.Take(2).Select(p => p[0])).ToUpperInvariant();
        }

        private static string ResolvePrimaryRole(IList<string> roles)
        {
            if (roles.Count == 0)
            {
                return "User";
            }

            string[] priority = { "Administrator", "Manager", "Technician", "Tenant" };
            foreach (var role in priority)
            {
                if (roles.Contains(role))
                {
                    return role;
                }
            }

            return roles[0];
        }
    }
}
