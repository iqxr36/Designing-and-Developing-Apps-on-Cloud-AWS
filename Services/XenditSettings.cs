namespace CloudMVCApplication.Services
{
    public class XenditSettings
    {
        public const string SectionName = "Xendit";

        public string ApiKey { get; set; } = string.Empty;

        public string CallbackToken { get; set; } = string.Empty;

        public string Currency { get; set; } = "MYR";

        public bool UseTestMode { get; set; } = true;

        public bool UseMockMode { get; set; } = false;

        public string SuccessRedirectBaseUrl { get; set; } = string.Empty;

        public string FailureRedirectBaseUrl { get; set; } = string.Empty;

        public string WebhookBaseUrl { get; set; } = string.Empty;

        public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

        public bool IsConfigured => HasApiKey || UseMockMode;

        public bool ShouldUseMock => UseMockMode && !HasApiKey;
    }
}
