namespace CloudMVCApplication.Models
{
    public static class TechnicianPayoutStatuses
    {
        public const string Pending = "Pending";
        public const string Processing = "Processing";
        public const string Paid = "Paid";
        public const string Failed = "Failed";
        public const string Cancelled = "Cancelled";
        public const string AwaitingTransfer = "AwaitingTransfer";
        public const string Transferred = "Transferred";

        public static string GetDisplayStatus(string? paymentStatus, string? technicianPayoutStatus)
        {
            if (paymentStatus == ServicePaymentStatuses.Pending)
            {
                return "Awaiting release";
            }

            if (paymentStatus == ServicePaymentStatuses.Processing)
            {
                return "Processing company charge";
            }

            if (paymentStatus == ServicePaymentStatuses.Failed)
            {
                return "Company charge failed";
            }

            if (paymentStatus == ServicePaymentStatuses.Paid)
            {
                return technicianPayoutStatus switch
                {
                    Transferred => "Paid to bank",
                    AwaitingTransfer => "Awaiting bank transfer",
                    _ => "Company charged"
                };
            }

            return paymentStatus ?? "Unknown";
        }
    }
}
