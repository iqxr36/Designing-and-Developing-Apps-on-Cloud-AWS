namespace CloudMVCApplication.ViewModels
{
    public record MetricCardViewModel(string Label, string Value, string? Note = null, string? Icon = null, string? BadgeClass = null);

    public record UserRowViewModel(
        string Id,
        string FullName,
        string Email,
        string? PhoneNumber,
        string Role,
        string Status,
        string Initials);

    public record PropertyCardViewModel(
        int PropertyId,
        string Name,
        string Address,
        string City,
        string Type,
        string Status,
        string? ImageUrl,
        int UnitCount,
        int OccupiedUnits);

    public record UnitRowViewModel(
        int UnitId,
        string UnitNumber,
        string PropertyName,
        string UnitType,
        string Status,
        string? TenantName,
        string? PropertyAddress = null,
        string? PropertyCity = null,
        int? FloorNumber = null);

    public record PropertyManagerRowViewModel(
        int ManagerId,
        string UserId,
        string FullName,
        string Email,
        string? PhoneNumber,
        string JobTitle,
        string Region,
        int? PropertyId,
        string? PropertyName);

    public record RequestRowViewModel(
        int RequestId,
        string DisplayId,
        string Title,
        string? Description,
        string Category,
        string Priority,
        string Status,
        DateTime CreatedAt,
        string TenantName,
        string PropertyName,
        string UnitNumber,
        string? TechnicianName,
        int? TechnicianId = null);

    public record TechnicianRowViewModel(
        int TechnicianId,
        string FullName,
        string Email,
        string Specialization,
        string AvailabilityStatus,
        int ActiveJobs,
        int CompletedJobs,
        decimal AverageRating,
        int MaxActiveJobs,
        int WorkloadPercentage,
        int AvailabilityPercentage);

    public record TechnicianWorkloadSnapshot(
        int ActiveJobs,
        int MaxActiveJobs,
        int WorkloadPercentage,
        int AvailabilityPercentage,
        string AvailabilityStatus);

    public record NotificationRowViewModel(
        int NotificationId,
        string Title,
        string? Message,
        bool IsRead,
        DateTime CreatedAt,
        int? RequestId = null);

    public record RequestImageRowViewModel(
        int ImageId,
        string ImageUrl,
        string ImageType,
        string UploadedByName,
        DateTime UploadedAt);

    public record RequestStatusHistoryRowViewModel(
        int HistoryId,
        string? OldStatus,
        string? NewStatus,
        string? Notes,
        string ChangedByName,
        DateTime ChangedAt);

    public record AuditLogRowViewModel(
        int AuditLogId,
        string UserName,
        string Action,
        string? EntityName,
        string? EntityId,
        string? Description,
        DateTime CreatedAt);

    public class AreaDashboardViewModel
    {
        public IReadOnlyList<MetricCardViewModel> Metrics { get; set; } = Array.Empty<MetricCardViewModel>();
        public IReadOnlyList<UserRowViewModel> Users { get; set; } = Array.Empty<UserRowViewModel>();
        public IReadOnlyList<PropertyCardViewModel> Properties { get; set; } = Array.Empty<PropertyCardViewModel>();
        public IReadOnlyList<UnitRowViewModel> Units { get; set; } = Array.Empty<UnitRowViewModel>();
        public IReadOnlyList<PropertyManagerRowViewModel> Managers { get; set; } = Array.Empty<PropertyManagerRowViewModel>();
        public IReadOnlyList<RequestRowViewModel> Requests { get; set; } = Array.Empty<RequestRowViewModel>();
        public IReadOnlyList<TechnicianRowViewModel> Technicians { get; set; } = Array.Empty<TechnicianRowViewModel>();
        public IReadOnlyList<NotificationRowViewModel> Notifications { get; set; } = Array.Empty<NotificationRowViewModel>();
        public IReadOnlyList<AuditLogRowViewModel> AuditLogs { get; set; } = Array.Empty<AuditLogRowViewModel>();
        public UserRowViewModel? SelectedUser { get; set; }
        public PropertyCardViewModel? SelectedProperty { get; set; }
        public UnitRowViewModel? SelectedUnit { get; set; }
        public RequestRowViewModel? SelectedRequest { get; set; }
        public IReadOnlyList<PropertyManagerRowViewModel> SelectedPropertyManagers { get; set; } = Array.Empty<PropertyManagerRowViewModel>();
        public IReadOnlyList<PropertyManagerRowViewModel> AvailableManagers { get; set; } = Array.Empty<PropertyManagerRowViewModel>();
        public IReadOnlyList<UnitRowViewModel> SelectedPropertyUnits { get; set; } = Array.Empty<UnitRowViewModel>();
        public IReadOnlyList<UserRowViewModel> SelectedPropertyTenants { get; set; } = Array.Empty<UserRowViewModel>();
        public IReadOnlyList<RequestImageRowViewModel> SelectedRequestImages { get; set; } = Array.Empty<RequestImageRowViewModel>();
        public IReadOnlyList<RequestStatusHistoryRowViewModel> SelectedRequestStatusHistories { get; set; } = Array.Empty<RequestStatusHistoryRowViewModel>();
        public ManagerPaymentReleaseViewModel? SelectedRequestPayment { get; set; }
        public TechnicianPayoutStatusViewModel? PayoutStatus { get; set; }
        public AuditLogRowViewModel? SelectedAuditLog { get; set; }
        public string? CurrentCompanyName { get; init; }
        public string CurrentUserName { get; init; } = string.Empty;
        public string CurrentUserEmail { get; init; } = string.Empty;
        public string? CurrentUserPhoneNumber { get; init; }
        public string CurrentUserInitials { get; init; } = string.Empty;
        public string? CurrentUserAvatarUrl { get; init; }
    }
}
