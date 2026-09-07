using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.CompilerServices;
using CloudMVCApplication.Data;
using CloudMVCApplication.Models;
using CloudMVCApplication.Services;
using CloudMVCApplication.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace CloudMVCApplication.Areas.Administrator.Controllers
{
    [Authorize(Roles = "Administrator")]
    [Area("Administrator")]
    [Route("[area]/[action]")]
    public class AdministratorController : Controller
    {
        private static readonly string[] AdminManagedRoles = { "Administrator", "Manager", "Tenant" };
        private static readonly string[] RequestStatuses = { "Pending", "Assigned", "In Progress", "On Hold", "Completed" };
        private const int PageSize = 10;

        private readonly ApplicationDbContext _context;
        private readonly DashboardDataService _dashboardData;
        private readonly MessagingService _messagingService;
        private readonly IFileStorageService _fileStorage;
        private readonly IAvatarService _avatarService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly SubscriptionLimitService _subscriptionLimits;

        public AdministratorController(
            ApplicationDbContext context,
            DashboardDataService dashboardData,
            MessagingService messagingService,
            IFileStorageService fileStorage,
            IAvatarService avatarService,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            SignInManager<ApplicationUser> signInManager,
            SubscriptionLimitService subscriptionLimits)
        {
            _context = context;
            _dashboardData = dashboardData;
            _messagingService = messagingService;
            _fileStorage = fileStorage;
            _avatarService = avatarService;
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _subscriptionLimits = subscriptionLimits;
        }

        public Task<IActionResult> Dashboard() => DashboardWithUsageAsync();

        private async Task<IActionResult> DashboardWithUsageAsync()
        {
            await SetSubscriptionUsageViewBagAsync();
            return await AdministratorViewAsync(nameof(Dashboard));
        }

        // Lists company users with search/filter paging for the administrator user-management screen.
        public async Task<IActionResult> Users(string? search, string? role, string? status, int pageNumber = 1)
        {
            var model = await BuildAdministratorModelAsync();
            var users = model.Users.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                users = users.Where(u =>
                    u.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    u.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (u.PhoneNumber?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                users = users.Where(u => u.Role == role);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                users = users.Where(u => u.Status == status);
            }

            var filteredUsers = users.ToList();
            var totalItems = filteredUsers.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
            pageNumber = Math.Clamp(pageNumber, 1, totalPages);
            model.Users = filteredUsers.Skip((pageNumber - 1) * PageSize).Take(PageSize).ToList();
            ViewBag.Search = search;
            ViewBag.Role = role;
            ViewBag.Status = status;
            ViewBag.Page = pageNumber;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            return View("~/Areas/Administrator/Views/Users.cshtml", model);
        }
        public async Task<IActionResult> UserDetail(string id)
        {
            var model = await BuildAdministratorModelAsync();
            model.SelectedUser = model.Users.FirstOrDefault(u => u.Id == id);

            if (model.SelectedUser == null)
            {
                return NotFound();
            }

            var companyId = await GetCurrentCompanyIdAsync();
            var canAddAdmin = await _subscriptionLimits.CanAddAdministratorAsync(companyId, id);
            ViewBag.CanAssignAdministratorRole = model.SelectedUser.Role == "Administrator" || canAddAdmin.Allowed;

            return View("~/Areas/Administrator/Views/UserDetail.cshtml", model);
        }

        public async Task<IActionResult> AddUser()
        {
            await SetAddUserViewBagAsync();
            return await AdministratorViewAsync();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Creates administrator-managed accounts and their matching role profile records.
        public async Task<IActionResult> AddUser(AdminUserCreateViewModel input)
        {
            await SetAddUserViewBagAsync();

            if (!AdminManagedRoles.Contains(input.Role))
            {
                ModelState.AddModelError(nameof(input.Role), input.Role == "Technician"
                    ? "Technicians must register on the platform and are approved by the platform administrator."
                    : "Choose a valid admin-managed role.");
            }

            if (await _userManager.FindByEmailAsync(input.Email) != null)
            {
                ModelState.AddModelError(nameof(input.Email), "A user with this email already exists.");
            }

            if (!ModelState.IsValid)
            {
                return View("~/Areas/Administrator/Views/AddUser.cshtml", await BuildAdministratorModelAsync());
            }

            var companyId = await GetCurrentCompanyIdAsync();
            if (input.Role == "Administrator")
            {
                var adminLimit = await _subscriptionLimits.CanAddAdministratorAsync(companyId);
                if (!adminLimit.Allowed)
                {
                    ModelState.AddModelError(nameof(input.Role), adminLimit.Message);
                    return View("~/Areas/Administrator/Views/AddUser.cshtml", await BuildAdministratorModelAsync());
                }
            }

            if (input.Role == "Manager")
            {
                var managerLimit = await _subscriptionLimits.CanAddManagerAsync(companyId);
                if (!managerLimit.Allowed)
                {
                    ModelState.AddModelError(string.Empty, managerLimit.Message);
                    return View("~/Areas/Administrator/Views/AddUser.cshtml", await BuildAdministratorModelAsync());
                }
            }

            var user = new ApplicationUser
            {
                FullName = $"{input.FirstName.Trim()} {input.LastName.Trim()}".Trim(),
                UserName = input.Email,
                Email = input.Email,
                PhoneNumber = input.PhoneNumber,
                CompanyId = await GetCurrentCompanyIdAsync(),
                IsActive = input.Status == "Active",
                EmailConfirmed = true,
                PhoneNumberConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user, input.TemporaryPassword);
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View("~/Areas/Administrator/Views/AddUser.cshtml", await BuildAdministratorModelAsync());
            }

            await EnsureRoleExistsAsync(input.Role);
            await _userManager.AddToRoleAsync(user, input.Role);
            await EnsureRoleProfileAsync(user.Id, input.Role);
            await AddAuditLogAsync("CreatedUser", nameof(ApplicationUser), user.Id, $"Created {input.Role} account for {user.FullName}.");

            TempData["SuccessMessage"] = "User created successfully.";
            return RedirectToAction(nameof(Users));
        }

        public async Task<IActionResult> Properties(string? search, string? type, string? status)
        {
            var model = await BuildAdministratorModelAsync();
            var properties = model.Properties.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                properties = properties.Where(p =>
                    p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    p.Address.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    p.City.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                properties = properties.Where(p => p.Type == type);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                properties = properties.Where(p => p.Status == status);
            }

            model.Properties = properties.ToList();
            ViewBag.Search = search;
            ViewBag.Type = type;
            ViewBag.Status = status;
            return View("~/Areas/Administrator/Views/Properties.cshtml", model);
        }
        public async Task<IActionResult> PropertyDetail(int id)
        {
            var model = await BuildAdministratorModelAsync();
            PopulatePropertyDetailModel(model, id);

            return model.SelectedProperty == null
                ? NotFound()
                : View("~/Areas/Administrator/Views/PropertyDetail.cshtml", model);
        }

        public async Task<IActionResult> AddProperty()
        {
            await SetSubscriptionUsageViewBagAsync();
            return await AdministratorViewAsync();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(52_428_800)]
        // Creates a property after checking subscription limits and optional photo upload data.
        public async Task<IActionResult> AddProperty(AdminPropertyViewModel input)
        {
            if (!ModelState.IsValid)
            {
                return await AddPropertyViewAsync();
            }

            var companyId = await GetCurrentCompanyIdAsync();
            var propertyLimit = await _subscriptionLimits.CanAddPropertyAsync(companyId);
            if (!propertyLimit.Allowed)
            {
                ModelState.AddModelError(string.Empty, propertyLimit.Message);
                return await AddPropertyViewAsync();
            }

            if (input.InitialUnitCount > 0)
            {
                var unitLimit = await _subscriptionLimits.CanAddUnitsAsync(companyId, input.InitialUnitCount);
                if (!unitLimit.Allowed)
                {
                    ModelState.AddModelError(nameof(input.InitialUnitCount), unitLimit.Message);
                    return await AddPropertyViewAsync();
                }
            }

            var (imageUrl, photoError) = await SavePropertyPhotoAsync(input.PropertyPhoto, input.ImageUrl);
            if (photoError != null)
            {
                ModelState.AddModelError(string.Empty, photoError);
                return await AddPropertyViewAsync();
            }

            var property = new Property
            {
                CompanyId = companyId,
                PropertyName = input.PropertyName,
                PropertyType = input.PropertyType,
                ImageUrl = imageUrl,
                Address = input.Address,
                City = input.City,
                Status = input.Status
            };

            _context.Properties.Add(property);
            await _context.SaveChangesAsync();

            if (input.InitialUnitCount > 0)
            {
                for (var i = 1; i <= input.InitialUnitCount; i++)
                {
                    _context.Units.Add(new Unit
                    {
                        PropertyId = property.PropertyId,
                        UnitNumber = $"Unit-{i:D3}",
                        UnitType = "Standard",
                        Status = "Vacant"
                    });
                }

                await _context.SaveChangesAsync();
            }

            var unitNote = input.InitialUnitCount > 0
                ? $" with {input.InitialUnitCount} units"
                : string.Empty;
            await AddAuditLogAsync("CreatedProperty", nameof(Property), property.PropertyId.ToString(), $"Created property {property.PropertyName}{unitNote}.");

            TempData["SuccessMessage"] = input.InitialUnitCount > 0
                ? $"Property added successfully with {input.InitialUnitCount} units."
                : "Property added successfully.";
            return RedirectToAction(nameof(Properties));
        }

        public async Task<IActionResult> Units(string? search, int? propertyId, string? unitType, string? status, int pageNumber = 1)
        {
            var model = await BuildAdministratorModelAsync();
            ViewBag.PortfolioUnitCount = model.Units.Count;
            ViewBag.PortfolioVacancyRate = model.Units.Count == 0 ? 0 : model.Units.Count(u => u.Status == "Vacant") * 100 / model.Units.Count;
            ViewBag.PortfolioMaintenanceCount = model.Units.Count(u => u.Status == "Maintenance");
            var units = model.Units.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                units = units.Where(u =>
                    u.UnitNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    u.PropertyName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (u.TenantName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (propertyId.HasValue)
            {
                var propertyName = model.Properties.FirstOrDefault(p => p.PropertyId == propertyId.Value)?.Name;
                units = units.Where(u => u.PropertyName == propertyName);
            }

            if (!string.IsNullOrWhiteSpace(unitType))
            {
                units = units.Where(u => u.UnitType == unitType);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                units = units.Where(u => u.Status == status);
            }

            var filteredUnits = units.ToList();
            var totalItems = filteredUnits.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
            pageNumber = Math.Clamp(pageNumber, 1, totalPages);
            model.Units = filteredUnits.Skip((pageNumber - 1) * PageSize).Take(PageSize).ToList();
            ViewBag.Search = search;
            ViewBag.PropertyId = propertyId;
            ViewBag.UnitType = unitType;
            ViewBag.Status = status;
            ViewBag.Page = pageNumber;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            return View("~/Areas/Administrator/Views/Units.cshtml", model);
        }
        public async Task<IActionResult> UnitDetail(int id)
        {
            var model = await BuildAdministratorModelAsync();
            model.SelectedUnit = model.Units.FirstOrDefault(u => u.UnitId == id);

            return model.SelectedUnit == null
                ? NotFound()
                : View("~/Areas/Administrator/Views/UnitDetail.cshtml", model);
        }

        public async Task<IActionResult> AddUnit()
        {
            await SetSubscriptionUsageViewBagAsync();
            return await AdministratorViewAsync();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Creates a unit and optionally assigns an existing tenant to it.
        public async Task<IActionResult> AddUnit(AdminUnitViewModel input)
        {
            if (await GetCompanyPropertyAsync(input.PropertyId) == null)
            {
                ModelState.AddModelError(nameof(input.PropertyId), "Choose a valid property.");
            }

            if (!string.IsNullOrWhiteSpace(input.TenantUserId))
            {
                if (!await IsCompanyTenantAsync(input.TenantUserId))
                {
                    ModelState.AddModelError(nameof(input.TenantUserId), "Choose a tenant from your company.");
                }
                input.Status = "Occupied";
            }
            else if (input.Status == "Occupied")
            {
                ModelState.AddModelError(nameof(input.Status), "Occupied units must have an assigned tenant.");
            }

            if (!ModelState.IsValid)
            {
                await SetSubscriptionUsageViewBagAsync();
                return View("~/Areas/Administrator/Views/AddUnit.cshtml", await BuildAdministratorModelAsync());
            }

            var property = await GetCompanyPropertyAsync(input.PropertyId);
            if (property != null)
            {
                var unitLimit = await _subscriptionLimits.CanAddUnitsAsync(property.CompanyId, 1);
                if (!unitLimit.Allowed)
                {
                    ModelState.AddModelError(string.Empty, unitLimit.Message);
                    await SetSubscriptionUsageViewBagAsync();
                    return View("~/Areas/Administrator/Views/AddUnit.cshtml", await BuildAdministratorModelAsync());
                }
            }

            var unit = new Unit
            {
                PropertyId = input.PropertyId,
                UnitNumber = input.UnitNumber,
                UnitType = input.UnitType,
                FloorNumber = input.FloorNumber,
                Status = input.Status
            };

            _context.Units.Add(unit);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(input.TenantUserId))
            {
                await AssignTenantToUnitAsync(input.TenantUserId, unit.UnitId);
                unit.Status = "Occupied";
                await _context.SaveChangesAsync();
            }

            await AddAuditLogAsync("CreatedUnit", nameof(Unit), unit.UnitId.ToString(), $"Created unit {unit.UnitNumber}.");

            TempData["SuccessMessage"] = "Unit added successfully.";
            return RedirectToAction(nameof(Units));
        }

        [NonAction]
        // Lists maintenance requests using search/status/priority/property filters for the admin maintenance page.
        public async Task<IActionResult> Maintenance(
            string? priority,
            string? status,
            int? propertyId,
            int? technicianId,
            string? category,
            string? search,
            DateTime? from,
            DateTime? to)
        {
            var model = await BuildAdministratorModelAsync(includeMaintenanceData: true);
            var requests = model.Requests.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(priority))
            {
                requests = requests.Where(r => r.Priority == priority);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                requests = requests.Where(r => r.Status == status);
            }

            if (propertyId.HasValue)
            {
                var propertyName = model.Properties.FirstOrDefault(p => p.PropertyId == propertyId.Value)?.Name;
                requests = propertyName == null
                    ? Enumerable.Empty<RequestRowViewModel>()
                    : requests.Where(r => r.PropertyName == propertyName);
            }

            if (technicianId.HasValue)
            {
                requests = requests.Where(r => r.TechnicianId == technicianId.Value);
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                requests = requests.Where(r => r.Category == category);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                requests = requests.Where(r =>
                    r.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (r.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    r.DisplayId.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            if (from.HasValue)
            {
                requests = requests.Where(r => r.CreatedAt.Date >= from.Value.Date);
            }

            if (to.HasValue)
            {
                requests = requests.Where(r => r.CreatedAt.Date <= to.Value.Date);
            }

            model.Requests = requests.ToList();
            ViewBag.Priority = priority;
            ViewBag.Status = status;
            ViewBag.PropertyId = propertyId;
            ViewBag.TechnicianId = technicianId;
            ViewBag.Category = category;
            ViewBag.Search = search;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");
            return View("~/Areas/Administrator/Views/Maintenance.cshtml", model);
        }

        [NonAction]
        public async Task<IActionResult> MaintenanceBoard()
            => View("~/Areas/Administrator/Views/MaintenanceBoard.cshtml", await BuildAdministratorModelAsync(includeMaintenanceData: true));

        [NonAction]
        // Loads one company request with photos and timeline history for administrator review.
        public async Task<IActionResult> RequestDetail(int id)
        {
            var request = await GetCompanyRequestAsync(id);
            if (request == null)
            {
                return NotFound();
            }

            var model = await BuildAdministratorModelAsync(includeMaintenanceData: true);
            model.SelectedRequest = model.Requests.FirstOrDefault(r => r.RequestId == id);
            model.SelectedRequestImages = await _context.RequestImages
                .Include(i => i.UploadedByUser)
                .Where(i => i.RequestId == id)
                .OrderByDescending(i => i.UploadedAt)
                .Select(i => new RequestImageRowViewModel(i.ImageId, i.ImageUrl, i.ImageType, i.UploadedByUser.FullName, i.UploadedAt))
                .ToListAsync();
            model.SelectedRequestStatusHistories = await _context.RequestStatusHistories
                .Include(h => h.ChangedByUser)
                .Where(h => h.RequestId == id)
                .OrderByDescending(h => h.ChangedAt)
                .Select(h => new RequestStatusHistoryRowViewModel(h.HistoryId, h.OldStatus, h.NewStatus, h.Notes, h.ChangedByUser.FullName, h.ChangedAt))
                .ToListAsync();

            return model.SelectedRequest == null
                ? NotFound()
                : View("~/Areas/Administrator/Views/RequestDetail.cshtml", model);
        }

        [NonAction]
        public async Task<IActionResult> CreateRequest()
            => View("~/Areas/Administrator/Views/CreateRequest.cshtml", await BuildAdministratorModelAsync(includeMaintenanceData: true));

        [NonAction]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Creates a maintenance request on behalf of a tenant and optionally assigns a technician immediately.
        public async Task<IActionResult> CreateRequest(AdminMaintenanceRequestViewModel input)
        {
            var unit = await GetCompanyUnitAsync(input.UnitId);
            if (unit == null)
            {
                ModelState.AddModelError(nameof(input.UnitId), "Choose a unit from your company.");
            }
            else if (unit.TenantProfile == null)
            {
                ModelState.AddModelError(nameof(input.UnitId), "A maintenance request requires a unit with an assigned tenant.");
            }

            TechnicianProfile? technician = null;
            if (input.TechnicianId.HasValue)
            {
                technician = await _context.TechnicianProfiles
                    .Include(t => t.User)
                    .FirstOrDefaultAsync(t => t.TechnicianId == input.TechnicianId.Value && t.User.IsActive);
                if (technician == null)
                {
                    ModelState.AddModelError(nameof(input.TechnicianId), "Choose an active technician.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View("~/Areas/Administrator/Views/CreateRequest.cshtml", await BuildAdministratorModelAsync(includeMaintenanceData: true));
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var request = new MaintenanceRequest
            {
                TenantId = unit!.TenantProfile!.TenantId,
                UnitId = unit.UnitId,
                PropertyId = unit.PropertyId,
                Title = input.Title.Trim(),
                Description = input.Description.Trim(),
                Category = input.Category,
                Priority = input.Priority,
                Status = technician == null ? "Pending" : "Assigned",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.MaintenanceRequests.Add(request);
            await _context.SaveChangesAsync();

            if (technician != null)
            {
                _context.Assignments.Add(new Assignment
                {
                    RequestId = request.RequestId,
                    TechnicianId = technician.TechnicianId,
                    AssignedByManagerId = currentUser.Id,
                    AssignedDate = DateTime.UtcNow,
                    Status = "Assigned"
                });
                _context.Notifications.Add(new Notification
                {
                    UserId = technician.UserId,
                    RequestId = request.RequestId,
                    Title = "New maintenance assignment",
                    Message = $"{request.Title} at {unit.Property.PropertyName}, unit {unit.UnitNumber}."
                });
            }

            _context.RequestStatusHistories.Add(new RequestStatusHistory
            {
                RequestId = request.RequestId,
                ChangedByUserId = currentUser.Id,
                OldStatus = null,
                NewStatus = request.Status,
                Notes = "Request created by administrator."
            });
            _context.Notifications.Add(new Notification
            {
                UserId = unit.TenantProfile.UserId,
                RequestId = request.RequestId,
                Title = "Maintenance request created",
                Message = $"Your request {FormatRequestId(request.RequestId)} was created by an administrator."
            });
            await _context.SaveChangesAsync();
            await AddAuditLogAsync("CreatedMaintenanceRequest", nameof(MaintenanceRequest), request.RequestId.ToString(), $"Created maintenance request {FormatRequestId(request.RequestId)}.");

            TempData["SuccessMessage"] = "Maintenance request created successfully.";
            return RedirectToAction(nameof(RequestSubmitted), new { id = request.RequestId });
        }

        [NonAction]
        public async Task<IActionResult> RequestSubmitted(int id)
        {
            var request = await GetCompanyRequestAsync(id);
            if (request == null)
            {
                return NotFound();
            }

            var model = await BuildAdministratorModelAsync(includeMaintenanceData: true);
            model.SelectedRequest = model.Requests.FirstOrDefault(r => r.RequestId == id);
            return View("~/Areas/Administrator/Views/RequestSubmitted.cshtml", model);
        }

        public async Task<IActionResult> Reports(string timePeriod = "30days", int? propertyId = null, string? category = null)
        {
            var model = await BuildAdministratorModelAsync(includeMaintenanceData: true);
            model.Requests = FilterReportRequests(model, timePeriod, propertyId, category).ToList();
            SetReportFilterViewBag(timePeriod, propertyId, category);
            return View("~/Areas/Administrator/Views/Reports.cshtml", model);
        }
        public Task<IActionResult> ReportDetail() => AdministratorReportsViewAsync();
        public async Task<IActionResult> GenerateReport(string timePeriod = "30days", int? propertyId = null, string? category = null)
        {
            SetReportFilterViewBag(timePeriod, propertyId, category);
            return View("~/Areas/Administrator/Views/GenerateReport.cshtml", await BuildAdministratorModelAsync(includeMaintenanceData: true));
        }

        [HttpGet]
        public async Task<IActionResult> TechnicianPayments()
        {
            var companyId = await GetCurrentCompanyIdAsync();
            var payments = await _context.TechnicianPayments
                .Include(p => p.MaintenanceRequest)
                .Include(p => p.Manager)
                .Include(p => p.Technician)
                    .ThenInclude(t => t.User)
                .Include(p => p.Company)
                .Where(p => p.CompanyId == companyId)
                .OrderByDescending(p => p.PaidAt ?? p.CreatedAt)
                .Select(p => new TechnicianPaymentReportRowViewModel(
                    p.Id,
                    p.MaintenanceRequestId,
                    $"REQ-{p.MaintenanceRequestId:0000}",
                    p.MaintenanceRequest.Title,
                    p.Manager.FullName,
                    p.Technician.User.FullName,
                    p.Company.CompanyName,
                    p.GrossAmount,
                    p.PlatformCommissionAmount,
                    p.TechnicianNetAmount,
                    p.PaymentStatus,
                    p.PaidAt,
                    p.TransactionReference))
                .ToListAsync();

            return View("~/Areas/Administrator/Views/TechnicianPayments.cshtml", new TechnicianPaymentReportViewModel
            {
                Payments = payments,
                TotalGrossAmount = payments.Sum(p => p.GrossAmount),
                TotalPlatformCommission = payments.Sum(p => p.PlatformCommissionAmount),
                TotalTechnicianNetAmount = payments.Sum(p => p.TechnicianNetAmount),
                PaymentCount = payments.Count
            });
        }

        public async Task<IActionResult> ExportReport(string type = "maintenance", string timePeriod = "30days", int? propertyId = null, string? category = null)
        {
            var csv = new StringBuilder();
            var fileName = $"{type}-report-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

            if (type == "technicians")
            {
                csv.AppendLine("Technician,Email,Specialization,Availability,Active Jobs,Completed Jobs,Average Rating");
                foreach (var technician in (await BuildAdministratorModelAsync(includeMaintenanceData: true)).Technicians)
                {
                    csv.AppendLine(ToCsv(technician.FullName, technician.Email, technician.Specialization, technician.AvailabilityStatus, technician.ActiveJobs.ToString(), technician.CompletedJobs.ToString(), technician.AverageRating.ToString("0.0")));
                }
            }
            else if (type == "units")
            {
                csv.AppendLine("Unit,Property,Type,Status,Tenant");
                var model = await BuildAdministratorModelAsync();
                var propertyName = propertyId.HasValue ? model.Properties.FirstOrDefault(p => p.PropertyId == propertyId)?.Name : null;
                foreach (var unit in model.Units.Where(u => propertyName == null || u.PropertyName == propertyName))
                {
                    csv.AppendLine(ToCsv(unit.UnitNumber, unit.PropertyName, unit.UnitType, unit.Status, unit.TenantName ?? ""));
                }
            }
            else
            {
                csv.AppendLine("Request ID,Title,Category,Priority,Status,Created,Tenant,Property,Unit,Technician");
                var model = await BuildAdministratorModelAsync(includeMaintenanceData: true);
                foreach (var request in FilterReportRequests(model, timePeriod, propertyId, category))
                {
                    csv.AppendLine(ToCsv(request.DisplayId, request.Title, request.Category, request.Priority, request.Status, request.CreatedAt.ToString("u"), request.TenantName, request.PropertyName, request.UnitNumber, request.TechnicianName ?? ""));
                }
            }

            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
        }

        public async Task<IActionResult> AuditLogs(string? user, string? actionFilter)
        {
            var model = await BuildAdministratorModelAsync();
            var companyId = await GetCurrentCompanyIdAsync();
            var query = _context.AuditLogs
                .Include(a => a.User)
                .Where(a => a.User.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(user))
            {
                query = query.Where(l => l.User.FullName.Contains(user));
            }

            if (!string.IsNullOrWhiteSpace(actionFilter))
            {
                query = query.Where(l => l.Action.Contains(actionFilter));
            }

            model.AuditLogs = await query
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

            ViewBag.User = user;
            ViewBag.ActionFilter = actionFilter;
            return View("~/Areas/Administrator/Views/AuditLogs.cshtml", model);
        }

        public async Task<IActionResult> ExportAuditLogs(string? user, string? actionFilter)
        {
            var companyId = await GetCurrentCompanyIdAsync();
            var query = _context.AuditLogs
                .Include(a => a.User)
                .Where(a => a.User.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(user))
            {
                query = query.Where(l => l.User.FullName.Contains(user));
            }

            if (!string.IsNullOrWhiteSpace(actionFilter))
            {
                query = query.Where(l => l.Action.Contains(actionFilter));
            }

            var logs = await query
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new
                {
                    l.CreatedAt,
                    l.User.FullName,
                    l.Action,
                    l.EntityName,
                    l.EntityId,
                    l.Description
                })
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Timestamp,User,Action,Entity,EntityId,Description");
            foreach (var log in logs)
            {
                csv.AppendLine(ToCsv(
                    log.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    log.FullName,
                    log.Action,
                    log.EntityName ?? string.Empty,
                    log.EntityId ?? string.Empty,
                    log.Description ?? string.Empty));
            }

            var fileName = $"audit-logs-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
        }

        public async Task<IActionResult> AuditLogDetail(int id)
        {
            var companyId = await GetCurrentCompanyIdAsync();
            var log = await _context.AuditLogs
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.AuditLogId == id && a.User.CompanyId == companyId);

            if (log == null)
            {
                return NotFound();
            }

            var model = await BuildAdministratorModelAsync();
            model.SelectedAuditLog = new AuditLogRowViewModel(
                log.AuditLogId,
                log.User.FullName,
                log.Action,
                log.EntityName,
                log.EntityId,
                log.Description,
                log.CreatedAt);

            return View("~/Areas/Administrator/Views/AuditLogDetail.cshtml", model);
        }

        public async Task<IActionResult> Settings()
        {
            var model = await BuildAdministratorModelAsync();
            ViewBag.Company = await GetCurrentCompanyAsync();
            await SetSubscriptionUsageViewBagAsync();
            return View("~/Areas/Administrator/Views/Settings.cshtml", model);
        }

        public Task<IActionResult> QuickActions() => AdministratorViewAsync();
        public Task<IActionResult> Notifications() => AdministratorViewAsync();
        public Task<IActionResult> Profile() => AdministratorViewAsync();
        public Task<IActionResult> ChangeAvatar() => AdministratorViewAsync();
        [NonAction]
        public async Task<IActionResult> AdvancedFilters()
            => View("~/Areas/Administrator/Views/AdvancedFilters.cshtml", await BuildAdministratorModelAsync(includeMaintenanceData: true));
        public Task<IActionResult> ExportReady() => AdministratorViewAsync();

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
                return View("~/Areas/Administrator/Views/ChangeAvatar.cshtml", await BuildAdministratorModelAsync());
            }

            await AddAuditLogAsync("UpdatedAvatar", nameof(ApplicationUser), currentUser.Id, "Updated administrator avatar.");

            TempData["SuccessMessage"] = "Avatar updated successfully.";
            return RedirectToAction(nameof(Profile));
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

            await AddAuditLogAsync("RemovedAvatar", nameof(ApplicationUser), currentUser.Id, "Removed administrator avatar.");
            TempData["SuccessMessage"] = "Avatar removed successfully.";
            return RedirectToAction(nameof(Settings), new { section = "profile" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Marks one administrator notification read and optionally opens the related request.
        public async Task<IActionResult> MarkNotificationRead(int id, int? requestId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == currentUser!.Id);
            if (notification == null)
            {
                return NotFound();
            }

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return requestId.HasValue
                ? RedirectToAction(nameof(RequestDetail), new { id = requestId.Value })
                : RedirectToAction(nameof(Notifications));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Clears all unread notifications for the signed-in administrator.
        public async Task<IActionResult> MarkAllNotificationsRead()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var notifications = await _context.Notifications.Where(n => n.UserId == currentUser.Id && !n.IsRead).ToListAsync();
            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Notifications marked as read.";
            return RedirectToAction(nameof(Notifications));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Updates administrator profile fields and optionally changes the password.
        public async Task<IActionResult> UpdateProfile(AdminProfileUpdateViewModel input)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            input.FullName = (input.FullName ?? string.Empty).Trim();

            var wantsPasswordChange =
                !string.IsNullOrWhiteSpace(input.CurrentPassword) ||
                !string.IsNullOrWhiteSpace(input.NewPassword) ||
                !string.IsNullOrWhiteSpace(input.ConfirmPassword);

            if (wantsPasswordChange)
            {
                if (string.IsNullOrWhiteSpace(input.CurrentPassword))
                {
                    ModelState.AddModelError(nameof(input.CurrentPassword), "Enter your current password to change your password.");
                }

                if (string.IsNullOrWhiteSpace(input.NewPassword))
                {
                    ModelState.AddModelError(nameof(input.NewPassword), "Enter a new password.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View("~/Areas/Administrator/Views/Profile.cshtml", await BuildAdministratorModelAsync());
            }

            currentUser.FullName = input.FullName;
            currentUser.PhoneNumber = input.PhoneNumber;

            var result = await _userManager.UpdateAsync(currentUser);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View("~/Areas/Administrator/Views/Profile.cshtml", await BuildAdministratorModelAsync());
            }

            if (wantsPasswordChange)
            {
                var passwordResult = await _userManager.ChangePasswordAsync(currentUser, input.CurrentPassword!, input.NewPassword!);
                if (!passwordResult.Succeeded)
                {
                    foreach (var error in passwordResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }

                    return View("~/Areas/Administrator/Views/Profile.cshtml", await BuildAdministratorModelAsync());
                }

                await _signInManager.RefreshSignInAsync(currentUser);
            }

            await AddAuditLogAsync("UpdatedProfile", nameof(ApplicationUser), currentUser.Id, "Updated administrator profile.");

            TempData["SuccessMessage"] = wantsPasswordChange
                ? "Profile and password updated successfully."
                : "Profile updated successfully.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCompanySettings(AdminCompanySettingsViewModel input)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Company = await GetCurrentCompanyAsync();
                return View("~/Areas/Administrator/Views/Settings.cshtml", await BuildAdministratorModelAsync());
            }

            var company = await GetCurrentCompanyAsync();
            company.CompanyName = input.CompanyName;
            company.CompanyEmail = input.CompanyEmail;
            company.CompanyPhone = input.CompanyPhone;
            company.CompanyAddress = input.CompanyAddress;

            await _context.SaveChangesAsync();
            await AddAuditLogAsync("UpdatedCompanySettings", nameof(PropertyManagementCompany), company.CompanyId.ToString(), "Updated organization settings.");

            TempData["SuccessMessage"] = "Company settings updated successfully.";
            return RedirectToAction(nameof(Settings));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateNotificationSettings(AdminNotificationSettingsViewModel input)
        {
            var company = await GetCurrentCompanyAsync();
            company.UrgentMaintenanceAlerts = input.UrgentMaintenanceAlerts;
            company.WeeklyReportSummaries = input.WeeklyReportSummaries;
            await _context.SaveChangesAsync();
            await AddAuditLogAsync("UpdatedNotificationSettings", nameof(PropertyManagementCompany), company.CompanyId.ToString(), "Updated administrator notification preferences.");

            TempData["SuccessMessage"] = "Notification preferences saved successfully.";
            return RedirectToAction(nameof(Settings), new { section = "notifications" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBrandingSettings(AdminBrandingSettingsViewModel input)
        {
            var allowedThemes = new[] { "Enterprise Dark", "Classic Light" };
            if (!allowedThemes.Contains(input.SidebarTheme))
            {
                ModelState.AddModelError(nameof(input.SidebarTheme), "Choose a valid sidebar theme.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Company = await GetCurrentCompanyAsync();
                await SetSubscriptionUsageViewBagAsync();
                return View("~/Areas/Administrator/Views/Settings.cshtml", await BuildAdministratorModelAsync());
            }

            var company = await GetCurrentCompanyAsync();
            company.PrimaryColor = input.PrimaryColor.ToUpperInvariant();
            company.SidebarTheme = input.SidebarTheme;
            await _context.SaveChangesAsync();
            await AddAuditLogAsync("UpdatedBrandingSettings", nameof(PropertyManagementCompany), company.CompanyId.ToString(), "Updated administrator branding settings.");

            TempData["SuccessMessage"] = "Branding settings saved successfully.";
            return RedirectToAction(nameof(Settings), new { section = "branding" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Updates a company user's account details, active state, role, and role-specific profile.
        public async Task<IActionResult> UpdateUser(string id, AdminUserEditViewModel input)
        {
            var user = await GetCompanyUserAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            if (!AdminManagedRoles.Contains(input.Role))
            {
                ModelState.AddModelError(nameof(input.Role), input.Role == "Technician"
                    ? "Technicians must register on the platform and are approved by the platform administrator."
                    : "Choose a valid admin-managed role.");
            }

            var duplicateEmailUser = await _userManager.FindByEmailAsync(input.Email);
            if (duplicateEmailUser != null && duplicateEmailUser.Id != user.Id)
            {
                ModelState.AddModelError(nameof(input.Email), "Another user already has this email.");
            }

            if (!ModelState.IsValid)
            {
                var model = await BuildAdministratorModelAsync();
                model.SelectedUser = model.Users.FirstOrDefault(u => u.Id == id);
                var companyId = await GetCurrentCompanyIdAsync();
                var canAddAdmin = await _subscriptionLimits.CanAddAdministratorAsync(companyId, id);
                ViewBag.CanAssignAdministratorRole = model.SelectedUser?.Role == "Administrator" || canAddAdmin.Allowed;
                return View("~/Areas/Administrator/Views/UserDetail.cshtml", model);
            }

            if (input.Role == "Administrator")
            {
                var companyId = await GetCurrentCompanyIdAsync();
                var adminLimit = await _subscriptionLimits.CanAddAdministratorAsync(companyId, id);
                if (!adminLimit.Allowed)
                {
                    ModelState.AddModelError(nameof(input.Role), adminLimit.Message);
                    var model = await BuildAdministratorModelAsync();
                    model.SelectedUser = model.Users.FirstOrDefault(u => u.Id == id);
                    ViewBag.CanAssignAdministratorRole = false;
                    return View("~/Areas/Administrator/Views/UserDetail.cshtml", model);
                }
            }

            user.FullName = input.FullName;
            user.UserName = input.Email;
            user.Email = input.Email;
            user.PhoneNumber = input.PhoneNumber;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                var model = await BuildAdministratorModelAsync();
                model.SelectedUser = model.Users.FirstOrDefault(u => u.Id == id);
                return View("~/Areas/Administrator/Views/UserDetail.cshtml", model);
            }

            await EnsureRoleExistsAsync(input.Role);
            var existingRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, existingRoles.Where(AdminManagedRoles.Contains));
            await _userManager.AddToRoleAsync(user, input.Role);
            await EnsureRoleProfileAsync(user.Id, input.Role);
            await AddAuditLogAsync("UpdatedUser", nameof(ApplicationUser), user.Id, $"Updated account details for {user.FullName}.");

            TempData["SuccessMessage"] = "User updated successfully.";
            return RedirectToAction(nameof(UserDetail), new { id = user.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Activates or deactivates a company user without deleting their account history.
        public async Task<IActionResult> ToggleUserStatus(string id)
        {
            var user = await GetCompanyUserAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);
            await AddAuditLogAsync(user.IsActive ? "ActivatedUser" : "DeactivatedUser", nameof(ApplicationUser), user.Id, $"{(user.IsActive ? "Activated" : "Deactivated")} account for {user.FullName}.");

            TempData["SuccessMessage"] = user.IsActive ? "User activated successfully." : "User deactivated successfully.";
            return RedirectToAction(nameof(UserDetail), new { id = user.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(52_428_800)]
        // Updates property details and replaces or preserves the property photo reference.
        public async Task<IActionResult> UpdateProperty(int id, AdminPropertyViewModel input)
        {
            var property = await GetCompanyPropertyAsync(id);
            if (property == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                var model = await BuildAdministratorModelAsync();
                PopulatePropertyDetailModel(model, id);
                return View("~/Areas/Administrator/Views/PropertyDetail.cshtml", model);
            }

            var (imageUrl, photoError) = await SavePropertyPhotoAsync(input.PropertyPhoto, property.ImageUrl);
            if (photoError != null)
            {
                ModelState.AddModelError(string.Empty, photoError);
                var model = await BuildAdministratorModelAsync();
                PopulatePropertyDetailModel(model, id);
                return View("~/Areas/Administrator/Views/PropertyDetail.cshtml", model);
            }

            property.PropertyName = input.PropertyName;
            property.PropertyType = input.PropertyType;
            property.ImageUrl = imageUrl;
            property.Address = input.Address;
            property.City = input.City;
            property.Status = input.Status;

            await _context.SaveChangesAsync();
            await AddAuditLogAsync("UpdatedProperty", nameof(Property), property.PropertyId.ToString(), $"Updated property {property.PropertyName}.");

            TempData["SuccessMessage"] = "Property updated successfully.";
            return RedirectToAction(nameof(PropertyDetail), new { id = property.PropertyId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Assigns an unassigned manager profile to a property in the current company.
        public async Task<IActionResult> AssignManagerToProperty(int propertyId, int managerId)
        {
            var property = await GetCompanyPropertyAsync(propertyId);
            if (property == null)
            {
                return NotFound();
            }

            var manager = await _context.ManagerProfiles
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.ManagerId == managerId && m.User.CompanyId == property.CompanyId);

            if (manager == null)
            {
                TempData["ErrorMessage"] = "Choose a valid manager.";
                return RedirectToAction(nameof(PropertyDetail), new { id = propertyId });
            }

            if (manager.PropertyId == propertyId)
            {
                TempData["ErrorMessage"] = $"{manager.User.FullName} is already assigned to this property.";
                return RedirectToAction(nameof(PropertyDetail), new { id = propertyId });
            }

            if (manager.PropertyId.HasValue)
            {
                TempData["ErrorMessage"] = $"{manager.User.FullName} is already assigned to another property.";
                return RedirectToAction(nameof(PropertyDetail), new { id = propertyId });
            }

            manager.PropertyId = propertyId;
            await _context.SaveChangesAsync();
            await AddAuditLogAsync("AssignedManagerToProperty", nameof(Property), propertyId.ToString(), $"Assigned {manager.User.FullName} to {property.PropertyName}.");

            TempData["SuccessMessage"] = "Manager assigned successfully.";
            return RedirectToAction(nameof(PropertyDetail), new { id = propertyId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Removes a manager's property assignment while keeping the manager account active.
        public async Task<IActionResult> RemoveManagerFromProperty(int propertyId, int managerId)
        {
            var property = await GetCompanyPropertyAsync(propertyId);
            if (property == null)
            {
                return NotFound();
            }

            var manager = await _context.ManagerProfiles
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.ManagerId == managerId && m.User.CompanyId == property.CompanyId);

            if (manager == null || manager.PropertyId != propertyId)
            {
                TempData["ErrorMessage"] = "That manager is not assigned to this property.";
                return RedirectToAction(nameof(PropertyDetail), new { id = propertyId });
            }

            manager.PropertyId = null;
            await _context.SaveChangesAsync();
            await AddAuditLogAsync("RemovedManagerFromProperty", nameof(Property), propertyId.ToString(), $"Removed {manager.User.FullName} from property {propertyId}.");

            TempData["SuccessMessage"] = "Manager removed from this property.";
            return RedirectToAction(nameof(PropertyDetail), new { id = propertyId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Updates unit attributes and tenant assignment, including previous unit vacancy cleanup.
        public async Task<IActionResult> UpdateUnit(int id, AdminUnitViewModel input)
        {
            var unit = await GetCompanyUnitAsync(id);

            if (unit == null)
            {
                return NotFound();
            }

            if (await GetCompanyPropertyAsync(input.PropertyId) == null)
            {
                ModelState.AddModelError(nameof(input.PropertyId), "Choose a valid property.");
            }

            if (!string.IsNullOrWhiteSpace(input.TenantUserId))
            {
                if (!await IsCompanyTenantAsync(input.TenantUserId))
                {
                    ModelState.AddModelError(nameof(input.TenantUserId), "Choose a tenant from your company.");
                }
                input.Status = "Occupied";
            }
            else if (input.Status == "Occupied")
            {
                ModelState.AddModelError(nameof(input.Status), "Occupied units must have an assigned tenant.");
            }

            if (!ModelState.IsValid)
            {
                var model = await BuildAdministratorModelAsync();
                model.SelectedUnit = model.Units.FirstOrDefault(u => u.UnitId == id);
                return View("~/Areas/Administrator/Views/UnitDetail.cshtml", model);
            }

            unit.UnitNumber = input.UnitNumber;
            unit.PropertyId = input.PropertyId;
            unit.UnitType = input.UnitType;
            unit.FloorNumber = input.FloorNumber;
            unit.Status = input.Status;

            if (!string.IsNullOrWhiteSpace(input.TenantUserId))
            {
                await AssignTenantToUnitAsync(input.TenantUserId, unit.UnitId);
                unit.Status = "Occupied";
            }
            else if (unit.TenantProfile != null && input.Status != "Occupied")
            {
                _context.TenantProfiles.Remove(unit.TenantProfile);
            }

            await _context.SaveChangesAsync();
            await AddAuditLogAsync("UpdatedUnit", nameof(Unit), unit.UnitId.ToString(), $"Updated unit {unit.UnitNumber}.");

            TempData["SuccessMessage"] = "Unit updated successfully.";
            return RedirectToAction(nameof(UnitDetail), new { id = unit.UnitId });
        }

        [NonAction]
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Updates an admin-managed request, assignment, timeline history, and tenant/technician notifications.
        public async Task<IActionResult> UpdateRequestStatus(int id, AdminRequestStatusViewModel input)
        {
            var request = await GetCompanyRequestAsync(id);
            if (request == null)
            {
                return NotFound();
            }

            if (!RequestStatuses.Contains(input.Status))
            {
                ModelState.AddModelError(nameof(input.Status), "Choose a valid request status.");
            }

            TechnicianProfile? technician = null;
            if (input.TechnicianId.HasValue)
            {
                technician = await _context.TechnicianProfiles
                    .Include(t => t.User)
                    .FirstOrDefaultAsync(t => t.TechnicianId == input.TechnicianId.Value && t.User.IsActive);
                if (technician == null)
                {
                    ModelState.AddModelError(nameof(input.TechnicianId), "Choose an active technician.");
                }
            }

            var requiresTechnician = input.Status is "Assigned" or "In Progress" or "Completed";
            if (requiresTechnician && technician == null && request.Assignment == null)
            {
                ModelState.AddModelError(nameof(input.TechnicianId), "Assign a technician before using this status.");
            }

            if (!ModelState.IsValid)
            {
                var invalidModel = await BuildAdministratorModelAsync(includeMaintenanceData: true);
                invalidModel.SelectedRequest = invalidModel.Requests.FirstOrDefault(r => r.RequestId == id);
                return View("~/Areas/Administrator/Views/RequestDetail.cshtml", invalidModel);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var oldStatus = request.Status;
            request.Status = input.Status;
            request.UpdatedAt = DateTime.UtcNow;

            if (technician != null)
            {
                if (request.Assignment == null)
                {
                    request.Assignment = new Assignment
                    {
                        RequestId = request.RequestId,
                        TechnicianId = technician.TechnicianId,
                        AssignedByManagerId = currentUser.Id,
                        AssignedDate = DateTime.UtcNow,
                        Status = input.Status
                    };
                }
                else
                {
                    request.Assignment.TechnicianId = technician.TechnicianId;
                    request.Assignment.Status = input.Status;
                }

                _context.Notifications.Add(new Notification
                {
                    UserId = technician.UserId,
                    RequestId = request.RequestId,
                    Title = "Maintenance request updated",
                    Message = $"{FormatRequestId(request.RequestId)} is now {input.Status}."
                });
            }
            else if (request.Assignment != null)
            {
                request.Assignment.Status = input.Status;
            }

            if (request.Assignment != null)
            {
                request.Assignment.CompletedAt = input.Status == "Completed" ? DateTime.UtcNow : null;
            }

            _context.RequestStatusHistories.Add(new RequestStatusHistory
            {
                RequestId = request.RequestId,
                ChangedByUserId = currentUser.Id,
                OldStatus = oldStatus,
                NewStatus = input.Status,
                Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim()
            });
            _context.Notifications.Add(new Notification
            {
                UserId = request.Tenant.UserId,
                RequestId = request.RequestId,
                Title = "Maintenance status updated",
                Message = $"{FormatRequestId(request.RequestId)} changed from {oldStatus} to {input.Status}."
            });

            await _context.SaveChangesAsync();
            await AddAuditLogAsync("UpdatedMaintenanceRequest", nameof(MaintenanceRequest), request.RequestId.ToString(), $"Changed {FormatRequestId(request.RequestId)} from {oldStatus} to {input.Status}.");

            TempData["SuccessMessage"] = "Maintenance request updated successfully.";
            return RedirectToAction(nameof(RequestDetail), new { id = request.RequestId });
        }

        private ViewResult AdministratorView([CallerMemberName] string actionName = "")
        {
            return View($"~/Areas/Administrator/Views/{actionName}.cshtml");
        }

        private async Task<IActionResult> AdministratorViewAsync([CallerMemberName] string actionName = "")
        {
            var model = await BuildAdministratorModelAsync();
            return View($"~/Areas/Administrator/Views/{actionName}.cshtml", model);
        }

        private async Task<IActionResult> AdministratorReportsViewAsync([CallerMemberName] string actionName = "")
        {
            var model = await BuildAdministratorModelAsync(includeMaintenanceData: true);
            return View($"~/Areas/Administrator/Views/{actionName}.cshtml", model);
        }

        private async Task SetAddUserViewBagAsync()
        {
            var companyId = await GetCurrentCompanyIdAsync();
            ViewBag.CanAddAdministrator = (await _subscriptionLimits.CanAddAdministratorAsync(companyId)).Allowed;
        }

        private async Task SetSubscriptionUsageViewBagAsync()
        {
            var companyId = await GetCurrentCompanyIdAsync();
            var usage = await _subscriptionLimits.GetUsageAsync(companyId);
            ViewBag.SubscriptionUsage = usage;
            ViewBag.RemainingUnits = usage.RemainingUnits;
        }

        private async Task<IActionResult> AddPropertyViewAsync()
        {
            await SetSubscriptionUsageViewBagAsync();
            return View("~/Areas/Administrator/Views/AddProperty.cshtml", await BuildAdministratorModelAsync());
        }

        // Centralizes administrator page model loading; maintenance data is optional for lighter pages.
        private async Task<AreaDashboardViewModel> BuildAdministratorModelAsync(bool includeMaintenanceData = false)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var model = await _dashboardData.GetAdministratorDashboardAsync(currentUser?.Id, includeMaintenanceData);
            return model;
        }

        // Fills selected property, assigned managers, available managers, units, and tenants for property details.
        private static void PopulatePropertyDetailModel(AreaDashboardViewModel model, int propertyId)
        {
            model.SelectedProperty = model.Properties.FirstOrDefault(p => p.PropertyId == propertyId);

            if (model.SelectedProperty == null)
            {
                return;
            }

            model.SelectedPropertyManagers = model.Managers
                .Where(m => m.PropertyId == propertyId)
                .ToList();

            model.AvailableManagers = model.Managers
                .Where(m => !m.PropertyId.HasValue)
                .ToList();

            model.SelectedPropertyUnits = model.Units
                .Where(u => u.PropertyName == model.SelectedProperty.Name)
                .ToList();

            var tenantNames = model.SelectedPropertyUnits
                .Where(u => !string.IsNullOrWhiteSpace(u.TenantName))
                .Select(u => u.TenantName!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            model.SelectedPropertyTenants = model.Users
                .Where(u => u.Role == "Tenant" && tenantNames.Contains(u.FullName))
                .ToList();
        }

        private async Task<PropertyManagementCompany> GetCurrentCompanyAsync()
        {
            var companyId = await GetCurrentCompanyIdAsync();
            var company = await _context.PropertyManagementCompanies.FirstOrDefaultAsync(c => c.CompanyId == companyId);
            if (company == null)
            {
                throw new InvalidOperationException("No property management company exists.");
            }

            return company;
        }

        // Resolves the administrator's company scope, falling back to the seeded company for demo data.
        private async Task<int> GetCurrentCompanyIdAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.CompanyId != null)
            {
                return currentUser.CompanyId.Value;
            }

            if (currentUser != null)
            {
                throw new InvalidOperationException("Administrator account is not linked to a company.");
            }

            var companyId = await _context.PropertyManagementCompanies
                .OrderBy(c => c.CompanyId)
                .Select(c => c.CompanyId)
                .FirstOrDefaultAsync();

            if (companyId == 0)
            {
                throw new InvalidOperationException("No property management company exists. Register a company before adding admin data.");
            }

            return companyId;
        }

        // Loads a property only when it belongs to the administrator's company.
        private async Task<Property?> GetCompanyPropertyAsync(int propertyId)
        {
            var companyId = await GetCurrentCompanyIdAsync();
            return await _context.Properties.FirstOrDefaultAsync(
                p => p.PropertyId == propertyId && p.CompanyId == companyId);
        }

        // Loads a unit with tenant/property context while enforcing company ownership.
        private async Task<Unit?> GetCompanyUnitAsync(int unitId)
        {
            var companyId = await GetCurrentCompanyIdAsync();
            return await _context.Units
                .Include(u => u.TenantProfile)
                    .ThenInclude(t => t!.User)
                .Include(u => u.Property)
                .FirstOrDefaultAsync(u => u.UnitId == unitId && u.Property.CompanyId == companyId);
        }

        // Loads a maintenance request only when it belongs to the administrator's company.
        private async Task<MaintenanceRequest?> GetCompanyRequestAsync(int requestId)
        {
            var companyId = await GetCurrentCompanyIdAsync();
            return await _context.MaintenanceRequests
                .Include(r => r.Property)
                .Include(r => r.Unit)
                .Include(r => r.Tenant)
                    .ThenInclude(t => t.User)
                .Include(r => r.Assignment)
                    .ThenInclude(a => a!.Technician)
                        .ThenInclude(t => t.User)
                .FirstOrDefaultAsync(r => r.RequestId == requestId && r.Property.CompanyId == companyId);
        }

        // Applies report filters to the already projected request rows.
        private static IEnumerable<RequestRowViewModel> FilterReportRequests(
            AreaDashboardViewModel model,
            string timePeriod,
            int? propertyId,
            string? category)
        {
            var now = DateTime.UtcNow;
            var startDate = timePeriod switch
            {
                "quarter" => new DateTime(now.Year, ((now.Month - 1) / 3 * 3) + 1, 1, 0, 0, 0, DateTimeKind.Utc),
                "year" => new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                _ => now.AddDays(-30)
            };

            var requests = model.Requests.Where(r => r.CreatedAt >= startDate);
            if (propertyId.HasValue)
            {
                var propertyName = model.Properties.FirstOrDefault(p => p.PropertyId == propertyId.Value)?.Name;
                requests = propertyName == null
                    ? Enumerable.Empty<RequestRowViewModel>()
                    : requests.Where(r => r.PropertyName == propertyName);
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                requests = requests.Where(r => r.Category == category);
            }

            return requests;
        }

        private void SetReportFilterViewBag(string timePeriod, int? propertyId, string? category)
        {
            ViewBag.TimePeriod = timePeriod;
            ViewBag.PropertyId = propertyId;
            ViewBag.Category = category;
        }

        // Loads an editable company user while blocking self-edit and protected roles.
        private async Task<ApplicationUser?> GetCompanyUserAsync(string userId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null && string.Equals(userId, currentUser.Id, StringComparison.Ordinal))
            {
                return null;
            }

            var companyId = await GetCurrentCompanyIdAsync();
            var user = await _context.Users.FirstOrDefaultAsync(
                u => u.Id == userId && u.CompanyId == companyId);

            if (user == null)
            {
                return null;
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Administrator") || roles.Contains("Technician"))
            {
                return null;
            }

            return user;
        }

        private async Task<bool> IsCompanyTenantAsync(string userId)
        {
            var companyId = await GetCurrentCompanyIdAsync();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId);
            return user != null && await _userManager.IsInRoleAsync(user, "Tenant");
        }

        // Creates a role on demand for seeded/demo environments where roles may not exist yet.
        private async Task EnsureRoleExistsAsync(string roleName)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new ApplicationRole { Name = roleName, NormalizedName = roleName.ToUpperInvariant() });
            }
        }

        // Ensures each Identity role has its matching domain profile row.
        private async Task EnsureRoleProfileAsync(string userId, string roleName)
        {
            switch (roleName)
            {
                case "Administrator" when !await _context.AdministratorProfiles.AnyAsync(p => p.UserId == userId):
                    _context.AdministratorProfiles.Add(new AdministratorProfile { UserId = userId });
                    break;
                case "Manager" when !await _context.ManagerProfiles.AnyAsync(p => p.UserId == userId):
                    _context.ManagerProfiles.Add(new ManagerProfile { UserId = userId });
                    break;
                case "Technician" when !await _context.TechnicianProfiles.AnyAsync(p => p.UserId == userId):
                    _context.TechnicianProfiles.Add(new TechnicianProfile
                    {
                        UserId = userId,
                        AvailabilityStatus = "Available",
                        CreatedAt = DateTime.UtcNow
                    });
                    break;
            }

            await _context.SaveChangesAsync();
        }

        // Moves or creates a tenant profile for a unit and frees the tenant's previous unit when needed.
        private async Task AssignTenantToUnitAsync(string tenantUserId, int unitId)
        {
            var tenantUser = await _userManager.FindByIdAsync(tenantUserId);
            if (tenantUser == null)
            {
                throw new InvalidOperationException("Tenant user was not found.");
            }

            await EnsureRoleExistsAsync("Tenant");
            if (!await _userManager.IsInRoleAsync(tenantUser, "Tenant"))
            {
                await _userManager.AddToRoleAsync(tenantUser, "Tenant");
            }

            var existingTenantForUnit = await _context.TenantProfiles.FirstOrDefaultAsync(t => t.UnitId == unitId && t.UserId != tenantUserId);
            if (existingTenantForUnit != null)
            {
                _context.TenantProfiles.Remove(existingTenantForUnit);
            }

            var tenantProfile = await _context.TenantProfiles.FirstOrDefaultAsync(t => t.UserId == tenantUserId);
            if (tenantProfile == null)
            {
                _context.TenantProfiles.Add(new TenantProfile
                {
                    UserId = tenantUserId,
                    UnitId = unitId,
                    MoveInDate = DateTime.UtcNow,
                    Status = "Active"
                });
            }
            else
            {
                var previousUnit = await _context.Units.FirstOrDefaultAsync(u => u.UnitId == tenantProfile.UnitId);
                if (previousUnit != null && previousUnit.UnitId != unitId)
                {
                    previousUnit.Status = "Vacant";
                }

                tenantProfile.UnitId = unitId;
                tenantProfile.Status = "Active";
            }
        }

        private static string FormatRequestId(int requestId) => $"REQ-{requestId:0000}";

        private static string ToCsv(params string[] values)
        {
            return string.Join(",", values.Select(value => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\""));
        }

        // Writes an audit record for administrator actions.
        private async Task AddAuditLogAsync(string action, string entityName, string entityId, string description)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return;
            }

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = currentUser.Id,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                Description = description
            });

            await _context.SaveChangesAsync();
        }

        // Validates and stores an uploaded property photo in S3, or keeps a provided image URL.
        private async Task<(string? ImageUrl, string? Error)> SavePropertyPhotoAsync(IFormFile? propertyPhoto, string? imageUrl)
        {
            if (propertyPhoto == null || propertyPhoto.Length == 0)
            {
                return (string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim(), null);
            }

            var extension = Path.GetExtension(propertyPhoto.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            if (!allowedExtensions.Contains(extension))
            {
                return (null, "Please upload a JPG, PNG, GIF, or WebP image.");
            }

            if (propertyPhoto.Length > 10 * 1024 * 1024)
            {
                return (null, "Property photo must be 10MB or smaller.");
            }

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var storedPath = await _fileStorage.UploadAsync(
                propertyPhoto,
                "properties",
                fileName,
                HttpContext.RequestAborted);

            if (!string.IsNullOrWhiteSpace(imageUrl) &&
                imageUrl.StartsWith(S3FileStorageService.StorageRoutePrefix, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(imageUrl, storedPath, StringComparison.OrdinalIgnoreCase))
            {
                await _fileStorage.DeleteAsync(imageUrl, HttpContext.RequestAborted);
            }

            return (storedPath, null);
        }
    }
}
