using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CloudMVCApplication.Models;
using Microsoft.Extensions.Options;

namespace CloudMVCApplication.Services
{
    public record XenditInvoiceResult(
        string ProviderInvoiceId,
        string ExternalId,
        string CheckoutUrl,
        string Status,
        string RawResponse);

    public record XenditWebhookPayload(
        string? Id,
        string? ExternalId,
        string? Status,
        string? PaymentId,
        string? PaymentMethod,
        string? PaymentChannel,
        DateTime? PaidAt,
        JsonElement? Metadata,
        string RawJson);

    public interface IXenditPaymentService
    {
        Task<XenditInvoiceResult> CreateSubscriptionInvoiceAsync(SubscriptionPayment payment, CancellationToken cancellationToken = default);
        Task<XenditInvoiceResult> CreateTechnicianPaymentInvoiceAsync(TechnicianPayment payment, CancellationToken cancellationToken = default);
        bool VerifyWebhookToken(HttpRequest request);
        XenditWebhookPayload ParseWebhookPayload(string json);
    }

    public class XenditPaymentService : IXenditPaymentService
    {
        private const string InvoiceEndpoint = "https://api.xendit.co/v2/invoices";

        private readonly HttpClient _httpClient;
        private readonly XenditSettings _settings;
        private readonly AppSettings _appSettings;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<XenditPaymentService> _logger;

        public XenditPaymentService(
            HttpClient httpClient,
            IOptions<XenditSettings> settings,
            IOptions<AppSettings> appSettings,
            IHttpContextAccessor httpContextAccessor,
            ILogger<XenditPaymentService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _appSettings = appSettings.Value;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public Task<XenditInvoiceResult> CreateSubscriptionInvoiceAsync(
            SubscriptionPayment payment,
            CancellationToken cancellationToken = default)
        {
            var company = payment.Company;
            var subscription = payment.CompanySubscription;
            var plan = subscription.SubscriptionPlan;
            var externalId = $"SUB-{payment.Id}-{Guid.NewGuid():N}";
            var successUrl = BuildReturnUrl("/Payments/SubscriptionReturn", payment.Id, _settings.SuccessRedirectBaseUrl);
            var failureUrl = BuildReturnUrl("/Payments/SubscriptionReturn", payment.Id, _settings.FailureRedirectBaseUrl);

            return CreateInvoiceAsync(
                externalId,
                payment.GrossAmount,
                payment.Currency,
                $"Subscription payment - {plan.Name}",
                company.CompanyName,
                company.CompanyEmail,
                successUrl,
                failureUrl,
                new Dictionary<string, object?>
                {
                    ["paymentType"] = "Subscription",
                    ["subscriptionPaymentId"] = payment.Id,
                    ["companySubscriptionId"] = payment.CompanySubscriptionId,
                    ["companyId"] = payment.CompanyId
                },
                cancellationToken);
        }

        public Task<XenditInvoiceResult> CreateTechnicianPaymentInvoiceAsync(
            TechnicianPayment payment,
            CancellationToken cancellationToken = default)
        {
            var externalId = $"TECHPAY-{payment.Id}-{Guid.NewGuid():N}";
            var successUrl = BuildReturnUrl("/Manager/TechnicianPayments/Return", payment.Id, _settings.SuccessRedirectBaseUrl);
            var failureUrl = BuildReturnUrl("/Manager/TechnicianPayments/Return", payment.Id, _settings.FailureRedirectBaseUrl);

            return CreateInvoiceAsync(
                externalId,
                payment.GrossAmount,
                payment.Currency,
                $"Technician payment - Request #{payment.MaintenanceRequestId}",
                payment.Manager.FullName,
                payment.Manager.Email ?? string.Empty,
                successUrl,
                failureUrl,
                new Dictionary<string, object?>
                {
                    ["paymentType"] = "TechnicianPayment",
                    ["technicianPaymentId"] = payment.Id,
                    ["maintenanceRequestId"] = payment.MaintenanceRequestId,
                    ["managerId"] = payment.ManagerId,
                    ["technicianId"] = payment.TechnicianId,
                    ["companyId"] = payment.CompanyId
                },
                cancellationToken);
        }

        public bool VerifyWebhookToken(HttpRequest request)
        {
            var callbackToken = request.Headers["x-callback-token"].ToString();
            return !string.IsNullOrWhiteSpace(_settings.CallbackToken) &&
                string.Equals(_settings.CallbackToken.Trim(), callbackToken?.Trim(), StringComparison.Ordinal);
        }

        public XenditWebhookPayload ParseWebhookPayload(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return new XenditWebhookPayload(
                GetString(root, "id"),
                GetString(root, "external_id"),
                GetString(root, "status"),
                GetString(root, "payment_id"),
                GetString(root, "payment_method"),
                GetString(root, "payment_channel"),
                GetDateTime(root, "paid_at"),
                root.TryGetProperty("metadata", out var metadata) ? metadata.Clone() : null,
                json);
        }

        private async Task<XenditInvoiceResult> CreateInvoiceAsync(
            string externalId,
            decimal amount,
            string currency,
            string description,
            string customerName,
            string customerEmail,
            string successRedirectUrl,
            string failureRedirectUrl,
            IDictionary<string, object?> metadata,
            CancellationToken cancellationToken)
        {
            if (_settings.ShouldUseMock)
            {
                var providerInvoiceId = $"mock-{externalId}";
                var checkoutUrl = BuildMockCheckoutUrl(externalId, providerInvoiceId);
                return new XenditInvoiceResult(providerInvoiceId, externalId, checkoutUrl, "PENDING", "{}");
            }

            if (!_settings.HasApiKey)
            {
                throw new InvalidOperationException("Xendit:ApiKey is not configured. Set it with .NET user-secrets or enable UseMockMode only as a fallback.");
            }

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, InvoiceEndpoint);
            var credential = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_settings.ApiKey}:"));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", credential);
            httpRequest.Content = JsonContent.Create(new
            {
                external_id = externalId,
                amount,
                currency,
                description,
                customer = new
                {
                    given_names = string.IsNullOrWhiteSpace(customerName) ? "Customer" : customerName,
                    email = customerEmail
                },
                success_redirect_url = successRedirectUrl,
                failure_redirect_url = failureRedirectUrl,
                metadata
            });

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Xendit invoice creation failed with status {StatusCode}.", response.StatusCode);
                throw new HttpRequestException($"Xendit invoice creation failed with status {(int)response.StatusCode}.");
            }

            using var document = JsonDocument.Parse(rawResponse);
            var root = document.RootElement;
            var id = GetString(root, "id") ?? externalId;
            var invoiceUrl = GetString(root, "invoice_url") ?? throw new InvalidOperationException("Xendit response did not include invoice_url.");
            var status = GetString(root, "status") ?? "PENDING";

            return new XenditInvoiceResult(id, externalId, invoiceUrl, status, rawResponse);
        }

        private string BuildReturnUrl(string path, int paymentId, string configuredBaseUrl)
        {
            var baseUrl = configuredBaseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = _appSettings.BaseUrl;
            }

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                var request = _httpContextAccessor.HttpContext?.Request;
                baseUrl = request == null ? string.Empty : $"{request.Scheme}://{request.Host}";
            }

            return $"{baseUrl}{path}?paymentId={paymentId}";
        }

        private string BuildMockCheckoutUrl(string externalId, string providerInvoiceId)
        {
            var request = _httpContextAccessor.HttpContext?.Request;
            var baseUrl = request == null
                ? string.Empty
                : $"{request.Scheme}://{request.Host}";

            return $"{baseUrl}/api/webhooks/xendit/mock-checkout?external_id={Uri.EscapeDataString(externalId)}&id={Uri.EscapeDataString(providerInvoiceId)}";
        }

        private static string? GetString(JsonElement root, string propertyName) =>
            root.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
                ? value.GetString()
                : null;

        private static DateTime? GetDateTime(JsonElement root, string propertyName)
        {
            var value = GetString(root, propertyName);
            return DateTime.TryParse(value, out var date) ? date.ToUniversalTime() : null;
        }
    }
}
