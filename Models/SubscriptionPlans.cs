namespace CloudMVCApplication.Models
{
    public static class SubscriptionPlans
    {
        public const string Starter = "Starter";
        public const string Professional = "Professional";
        public const string Enterprise = "Enterprise";
        public const string Standard = Professional;
        public const string Business = Enterprise;

        public static readonly string[] All = { Starter, Professional, Enterprise };

        public static (int MaxProperties, int MaxUnits, int MaxManagers) GetLimits(string plan) =>
            plan switch
            {
                Starter => (1, 25, 2),
                Professional or "Standard" => (5, 150, 10),
                Enterprise or "Business" => (20, 500, int.MaxValue),
                _ => GetLimits(Professional)
            };

        public static bool IsUnlimitedManagers(int maxManagers) => maxManagers == int.MaxValue;

        public const int MaxAdministratorsPerCompany = 1;

        public static string GetDisplayPrice(string plan) =>
            plan switch
            {
                Starter => "RM 99",
                Professional or "Standard" => "RM 299",
                Enterprise or "Business" => "RM 599",
                _ => "—"
            };

        public static string GetPlanDescription(string plan)
        {
            var (maxProperties, maxUnits, maxManagers) = GetLimits(plan);
            var managers = IsUnlimitedManagers(maxManagers) ? "Unlimited" : maxManagers.ToString();
            return $"{maxProperties} properties - {maxUnits} units - {managers} managers";
        }

        public static decimal GetMonthlyPrice(string plan) =>
            plan switch
            {
                Starter => 99m,
                Professional or "Standard" => 299m,
                Enterprise or "Business" => 599m,
                _ => GetMonthlyPrice(Professional)
            };

        public static string Normalize(string plan) =>
            plan switch
            {
                "Standard" => Professional,
                "Business" => Enterprise,
                _ => All.Contains(plan) ? plan : Professional
            };
    }

    public static class CompanySubscriptionStatuses
    {
        public const string Active = "Active";
        public const string Trialing = "Trialing";
        public const string PendingPayment = "PendingPayment";
        public const string PastDue = "PastDue";
        public const string Suspended = "Suspended";
        public const string Cancelled = "Cancelled";

        public static readonly string[] All =
        {
            Active, Trialing, PendingPayment, PastDue, Suspended, Cancelled
        };
    }
}
