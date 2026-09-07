using System.Text.Json;
using CloudMVCApplication.Data;
using CloudMVCApplication.Models;
using CloudMVCApplication.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CloudMVCApplication.Controllers
{
    [ApiController]
    [Route("api/webhooks/xendit")]
    [AllowAnonymous]
    public class XenditWebhookController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IXenditPaymentService _xendit;
        private readonly TechnicianPaymentService _technicianPayments;
        private readonly CompanyBillingHistoryService _billingHistory;
        private readonly XenditSettings _settings;
        private readonly ILogger<XenditWebhookController> _logger;

        public XenditWebhookController(
            ApplicationDbContext context,
            IXenditPaymentService xendit,
            TechnicianPaymentService technicianPayments,
            CompanyBillingHistoryService billingHistory,
            IOptions<XenditSettings> settings,
            ILogger<XenditWebhookController> logger)
        {
            _context = context;
            _xendit = xendit;
            _technicianPayments = technicianPayments;
            _billingHistory = billingHistory;
            _settings = settings.Value;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Handle()
        {
            if (!_xendit.VerifyWebhookToken(Request))
            {
                return Unauthorized();
            }

            using var reader = new StreamReader(Request.Body);
            var rawJson = await reader.ReadToEndAsync();
            var payload = _xendit.ParseWebhookPayload(rawJson);
            var externalId = payload.ExternalId;

            if (string.IsNullOrWhiteSpace(externalId))
            {
                return BadRequest("Missing external_id.");
            }

            await ProcessAsync(payload, DateTime.UtcNow);
            return Ok();
        }

        [HttpPost("mock-complete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MockComplete(
            [FromForm] string external_id,
            [FromForm] string? id,
            [FromForm] string cardNumber,
            [FromForm] string expiry,
            [FromForm] string cvc,
            [FromForm] string cardholderName)
        {
            if (!_settings.ShouldUseMock)
            {
                return Unauthorized();
            }

            var validationError = ValidateMockCard(cardNumber, expiry, cvc, cardholderName);
            if (validationError != null)
            {
                return MockCheckout(external_id, id, validationError);
            }

            var rawJson = JsonSerializer.Serialize(new
            {
                id,
                external_id,
                status = "PAID",
                payment_id = $"mock-pay-{Guid.NewGuid():N}",
                payment_method = "TEST_CARD",
                payment_channel = "MOCK",
                paid_at = DateTime.UtcNow
            });
            await ProcessAsync(_xendit.ParseWebhookPayload(rawJson), DateTime.UtcNow);

            if (external_id.StartsWith("TECHPAY-", StringComparison.OrdinalIgnoreCase) &&
                TryGetLocalId(external_id, "TECHPAY", out var technicianPaymentId))
            {
                var requestId = await _context.TechnicianPayments
                    .Where(p => p.Id == technicianPaymentId)
                    .Select(p => p.MaintenanceRequestId)
                    .FirstOrDefaultAsync();
                if (requestId > 0)
                {
                    return Redirect($"/Manager/RequestDetails/{requestId}");
                }
            }

            if (external_id.StartsWith("SUB-", StringComparison.OrdinalIgnoreCase) &&
                TryGetLocalId(external_id, "SUB", out var subscriptionPaymentId))
            {
                return Redirect($"/Payments/SubscriptionReturn?paymentId={subscriptionPaymentId}");
            }

            return Redirect("/Identity/Account/RegisterCompany?registered=true&billing=checkout_failed");
        }

        [HttpGet("mock-checkout")]
        public IActionResult MockCheckout(string external_id, string? id, string? error = null)
        {
            if (!_settings.ShouldUseMock)
            {
                return Unauthorized();
            }

            var completeUrl = Url.Action(
                nameof(MockComplete),
                "XenditWebhook",
                null,
                Request.Scheme) ?? "/api/webhooks/xendit/mock-complete";
            var errorMarkup = string.IsNullOrWhiteSpace(error)
                ? string.Empty
                : $"""<div class="error">{System.Net.WebUtility.HtmlEncode(error)}</div>""";

            var html = $$"""
                <!doctype html>
                <html lang="en">
                <head>
                    <meta charset="utf-8" />
                    <meta name="viewport" content="width=device-width, initial-scale=1" />
                    <title>Xendit Mock Checkout</title>
                    <style>
                        body { margin:0; min-height:100vh; display:grid; place-items:center; font-family:Arial, sans-serif; background:#f4f6f8; color:#111827; }
                        main { width:min(440px, calc(100% - 32px)); background:#fff; border:1px solid #d9dee7; border-radius:8px; padding:28px; box-shadow:0 20px 45px rgba(15,23,42,.08); }
                        h1 { margin:0 0 8px; font-size:24px; }
                        p { color:#5b6472; line-height:1.5; }
                        code { display:block; word-break:break-all; background:#f1f5f9; padding:12px; border-radius:6px; font-size:12px; color:#334155; }
                        label { display:block; margin-top:14px; font-size:13px; font-weight:700; color:#374151; }
                        input { width:100%; box-sizing:border-box; height:42px; margin-top:6px; padding:0 12px; border:1px solid #cfd7e3; border-radius:6px; font-size:15px; }
                        .row { display:grid; grid-template-columns:1fr 1fr; gap:12px; }
                        button { display:inline-flex; align-items:center; justify-content:center; width:100%; height:44px; margin-top:18px; border:0; border-radius:6px; background:#1778ff; color:#fff; font-weight:700; cursor:pointer; }
                        .error { padding:10px 12px; margin:14px 0 0; border-radius:6px; background:#fee2e2; color:#991b1b; font-size:13px; }
                        .note { font-size:13px; margin-top:14px; }
                    </style>
                </head>
                <body>
                    <main>
                        <h1>Xendit Mock Checkout</h1>
                        <p>This is demo mode. Use the test credit card below to confirm payment.</p>
                        <code>{{System.Net.WebUtility.HtmlEncode(external_id)}}</code>
                        {{errorMarkup}}
                        <form method="post" action="{{System.Net.WebUtility.HtmlEncode(completeUrl)}}">
                            {{AntiforgeryHtml()}}
                            <input type="hidden" name="external_id" value="{{System.Net.WebUtility.HtmlEncode(external_id)}}" />
                            <input type="hidden" name="id" value="{{System.Net.WebUtility.HtmlEncode(id ?? string.Empty)}}" />
                            <label for="cardholderName">Cardholder name</label>
                            <input id="cardholderName" name="cardholderName" value="Test User" autocomplete="cc-name" required />
                            <label for="cardNumber">Credit card number</label>
                            <input id="cardNumber" name="cardNumber" inputmode="numeric" autocomplete="cc-number" value="4000000000000002" required />
                            <div class="row">
                                <div>
                                    <label for="expiry">Expiry</label>
                                    <input id="expiry" name="expiry" autocomplete="cc-exp" value="12/30" required />
                                </div>
                                <div>
                                    <label for="cvc">CVC</label>
                                    <input id="cvc" name="cvc" inputmode="numeric" autocomplete="cc-csc" value="123" required />
                                </div>
                            </div>
                            <button type="submit">Pay with Test Credit Card</button>
                        </form>
                        <p class="note">Accepted test card: 4000 0000 0000 0002, any future expiry, any 3-digit CVC. No real card is charged.</p>
                    </main>
                </body>
                </html>
                """;

            return Content(html, "text/html");
        }

        private string AntiforgeryHtml()
        {
            var tokens = HttpContext.RequestServices
                .GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>()
                .GetAndStoreTokens(HttpContext);

            return $"""<input name="{tokens.FormFieldName}" type="hidden" value="{System.Net.WebUtility.HtmlEncode(tokens.RequestToken)}" />""";
        }

        private static string? ValidateMockCard(string cardNumber, string expiry, string cvc, string cardholderName)
        {
            var digits = new string((cardNumber ?? string.Empty).Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(cardholderName))
            {
                return "Enter the cardholder name.";
            }

            if (digits != "4000000000000002")
            {
                return "Use the test credit card number 4000 0000 0000 0002.";
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(expiry ?? string.Empty, @"^(0[1-9]|1[0-2])\/?([0-9]{2}|[0-9]{4})$"))
            {
                return "Enter a valid future expiry such as 12/30.";
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(cvc ?? string.Empty, @"^[0-9]{3,4}$"))
            {
                return "Enter a valid CVC.";
            }

            return null;
        }

        private async Task ProcessAsync(XenditWebhookPayload payload, DateTime processedAt)
        {
            var externalId = payload.ExternalId ?? string.Empty;
            var status = NormalizeStatus(payload.Status);
            var providerInvoiceId = payload.Id;
            var providerPaymentReference = payload.PaymentId ?? payload.PaymentChannel ?? payload.PaymentMethod;
            var paidAt = payload.PaidAt ?? processedAt;

            if (externalId.StartsWith("SUB-", StringComparison.OrdinalIgnoreCase) &&
                TryGetLocalId(externalId, "SUB", out var subscriptionPaymentId))
            {
                await ProcessSubscriptionPaymentAsync(subscriptionPaymentId, providerInvoiceId, providerPaymentReference, status, paidAt, payload.RawJson);
                return;
            }

            if (externalId.StartsWith("TECHPAY-", StringComparison.OrdinalIgnoreCase) &&
                TryGetLocalId(externalId, "TECHPAY", out var technicianPaymentId))
            {
                await ProcessTechnicianPaymentAsync(technicianPaymentId, providerInvoiceId, providerPaymentReference, status, paidAt, payload.RawJson);
                return;
            }

            _logger.LogWarning("Ignoring Xendit webhook with unknown external_id {ExternalId}.", externalId);
        }

        private async Task ProcessSubscriptionPaymentAsync(
            int paymentId,
            string? providerInvoiceId,
            string? providerPaymentReference,
            string status,
            DateTime processedAt,
            string rawPayload)
        {
            var payment = await _context.SubscriptionPayments
                .Include(p => p.Company)
                .Include(p => p.CompanySubscription)
                    .ThenInclude(s => s.SubscriptionPlan)
                .FirstOrDefaultAsync(p => p.Id == paymentId);
            if (payment == null)
            {
                return;
            }

            payment.ProviderInvoiceId ??= providerInvoiceId;
            payment.ProviderPaymentReference = providerPaymentReference;
            payment.RawWebhookPayload = rawPayload;
            payment.UpdatedAt = processedAt;

            if (IsPaidStatus(status) && payment.PaymentStatus != PaymentStatuses.Paid)
            {
                payment.PaymentStatus = PaymentStatuses.Paid;
                payment.PaidAt = processedAt;
                payment.CompanySubscription.PaymentStatus = PaymentStatuses.Paid;
                payment.CompanySubscription.Status = CompanySubscriptionRecordStatuses.Active;
                payment.CompanySubscription.StartDate = processedAt;
                payment.CompanySubscription.EndDate = CalculatePeriodEnd(processedAt, payment.CompanySubscription.SubscriptionPlan.BillingCycle);
                payment.CompanySubscription.UpdatedAt = processedAt;
                payment.Company.SubscriptionStatus = CompanySubscriptionStatuses.Active;
                payment.Company.Status = "Active";
                payment.Company.TrialEndsAt = null;
                payment.Company.CurrentPeriodStart = processedAt;
                payment.Company.CurrentPeriodEnd = payment.CompanySubscription.EndDate;

                await _billingHistory.RecordEventAsync(
                    payment.CompanyId,
                    CompanySubscriptionEventTypes.CheckoutCompleted,
                    providerEventId: providerInvoiceId,
                    newPlan: payment.Company.SubscriptionPlan,
                    newStatus: payment.Company.SubscriptionStatus,
                    amount: payment.GrossAmount,
                    currency: payment.Currency);
            }
            else if (status == "EXPIRED")
            {
                payment.PaymentStatus = PaymentStatuses.Expired;
                payment.CompanySubscription.PaymentStatus = PaymentStatuses.Expired;
                payment.CompanySubscription.Status = CompanySubscriptionRecordStatuses.Expired;
                payment.CompanySubscription.UpdatedAt = processedAt;
            }
            else if (status == "FAILED")
            {
                payment.PaymentStatus = PaymentStatuses.Failed;
                payment.CompanySubscription.PaymentStatus = PaymentStatuses.Failed;
                payment.CompanySubscription.UpdatedAt = processedAt;
            }

            await _context.SaveChangesAsync();
        }

        private async Task ProcessTechnicianPaymentAsync(
            int paymentId,
            string? providerInvoiceId,
            string? providerPaymentReference,
            string status,
            DateTime processedAt,
            string rawPayload)
        {
            var payment = await _context.TechnicianPayments.FirstOrDefaultAsync(p => p.Id == paymentId);
            if (payment == null)
            {
                return;
            }

            payment.ProviderInvoiceId ??= providerInvoiceId;
            payment.RawWebhookPayload = rawPayload;
            payment.UpdatedAt = processedAt;

            if (IsPaidStatus(status))
            {
                await _technicianPayments.MarkPaidFromProviderAsync(payment, providerPaymentReference, processedAt);
            }
            else if (status == "EXPIRED")
            {
                payment.PaymentStatus = TechnicianPaymentStatuses.Expired;
            }
            else if (status == "FAILED")
            {
                payment.PaymentStatus = TechnicianPaymentStatuses.Failed;
            }

            await _context.SaveChangesAsync();
        }

        private static string? GetString(JsonElement root, string propertyName) =>
            root.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
                ? value.GetString()
                : null;

        private static string NormalizeStatus(string? status) =>
            string.IsNullOrWhiteSpace(status) ? string.Empty : status.Trim().ToUpperInvariant();

        private static bool IsPaidStatus(string status) => status is "PAID" or "SETTLED";

        private static DateTime CalculatePeriodEnd(DateTime start, string? billingCycle) =>
            string.Equals(billingCycle, "Yearly", StringComparison.OrdinalIgnoreCase)
                ? start.AddYears(1)
                : start.AddMonths(1);

        private static bool TryGetLocalId(string externalId, string prefix, out int id)
        {
            id = 0;
            var parts = externalId.Split('-', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 &&
                string.Equals(parts[0], prefix, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(parts[1], out id);
        }
    }
}
