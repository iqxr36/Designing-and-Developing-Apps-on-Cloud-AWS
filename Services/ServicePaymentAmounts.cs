using CloudMVCApplication.Models;

namespace CloudMVCApplication.Services
{
    public static class ServicePaymentAmounts
    {
        public static decimal GetTechnicianNetAmount(decimal grossAmount, decimal platformFeeAmount) =>
            Math.Round(grossAmount - platformFeeAmount, 2, MidpointRounding.AwayFromZero);

        public static decimal GetTechnicianNetAmount(ServicePayment payment) =>
            GetTechnicianNetAmount(payment.Amount, payment.PlatformFeeAmount);
    }
}
