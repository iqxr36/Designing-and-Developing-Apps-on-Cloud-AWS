namespace CloudMVCApplication.ViewModels
{
    public record TechnicianPaymentReportRowViewModel(
        int Id,
        int MaintenanceRequestId,
        string RequestDisplayId,
        string RequestTitle,
        string ManagerName,
        string TechnicianName,
        string CompanyName,
        decimal GrossAmount,
        decimal PlatformCommissionAmount,
        decimal TechnicianNetAmount,
        string PaymentStatus,
        DateTime? PaidAt,
        string TransactionReference);

    public class TechnicianPaymentReportViewModel
    {
        public IReadOnlyList<TechnicianPaymentReportRowViewModel> Payments { get; set; } = Array.Empty<TechnicianPaymentReportRowViewModel>();
        public decimal TotalGrossAmount { get; set; }
        public decimal TotalPlatformCommission { get; set; }
        public decimal TotalTechnicianNetAmount { get; set; }
        public int PaymentCount { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int? CompanyId { get; set; }
        public IReadOnlyList<CompanyFilterOptionViewModel> Companies { get; set; } = Array.Empty<CompanyFilterOptionViewModel>();
    }

    public record CompanyFilterOptionViewModel(int CompanyId, string CompanyName);
}
