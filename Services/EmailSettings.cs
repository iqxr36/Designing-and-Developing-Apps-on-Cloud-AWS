namespace CloudMVCApplication.Services
{
    public class EmailSettings
    {
        public const string SectionName = "EmailSettings";

        public string SmtpHost { get; set; } = string.Empty;

        public int SmtpPort { get; set; } = 587;

        public string SmtpUsername { get; set; } = string.Empty;

        public string SmtpPassword { get; set; } = string.Empty;

        public string FromEmail { get; set; } = string.Empty;

        public string FromName { get; set; } = "ZamMaintain";

        public bool EnableSsl { get; set; } = true;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(SmtpHost) &&
            !string.IsNullOrWhiteSpace(SmtpUsername) &&
            !string.IsNullOrWhiteSpace(SmtpPassword) &&
            !string.IsNullOrWhiteSpace(FromEmail);
    }
}
