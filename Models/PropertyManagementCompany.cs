using System.ComponentModel.DataAnnotations;

namespace CloudMVCApplication.Models
{
    public class PropertyManagementCompany
    {
        [Key]
        public int CompanyId { get; set; }

        [Required]
        public string CompanyName { get; set; } = string.Empty;

        [Required]
        public string CompanyEmail { get; set; } = string.Empty;

        public string? CompanyPhone { get; set; }

        public string? CompanyAddress { get; set; }

        public bool UrgentMaintenanceAlerts { get; set; } = true;

        public bool WeeklyReportSummaries { get; set; } = true;

        public string PrimaryColor { get; set; } = "#570000";

        public string SidebarTheme { get; set; } = "Enterprise Dark";

        public string Status { get; set; } = "Active";

        public string SubscriptionPlan { get; set; } = SubscriptionPlans.Standard;

        public string SubscriptionStatus { get; set; } = CompanySubscriptionStatuses.Trialing;

        public string? XenditCustomerId { get; set; }

        public string? XenditSubscriptionId { get; set; }

        public string? LegacyCustomerId { get; set; }

        public string? LegacySubscriptionId { get; set; }

        public int MaxProperties { get; set; } = 5;

        public int MaxUnits { get; set; } = 150;

        public int MaxManagers { get; set; } = 10;

        public DateTime? CurrentPeriodStart { get; set; }

        public DateTime? CurrentPeriodEnd { get; set; }

        public DateTime? TrialEndsAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();

        public ICollection<Property> Properties { get; set; } = new List<Property>();

        public ICollection<CompanySubscriptionEvent> SubscriptionEvents { get; set; } = new List<CompanySubscriptionEvent>();

        public ICollection<CompanyInvoice> Invoices { get; set; } = new List<CompanyInvoice>();

        public ICollection<SubscriptionPayment> SubscriptionPayments { get; set; } = new List<SubscriptionPayment>();

        public ICollection<CompanySubscription> CompanySubscriptions { get; set; } = new List<CompanySubscription>();
    }
}
