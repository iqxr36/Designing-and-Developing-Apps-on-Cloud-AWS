using System.Net;

namespace CloudMVCApplication.Services
{
    public static class TechnicianNotificationEmailBuilder
    {
        public const string ApprovedSubject = "Your ZamMaintain technician account was approved";
        public const string RejectedSubject = "Your ZamMaintain technician application update";
        public const string WelcomeSubject = "Your ZamMaintain technician account is ready";

        public static string BuildApprovedHtml(string fullName, string loginUrl)
        {
            var name = WebUtility.HtmlEncode(fullName);
            var url = WebUtility.HtmlEncode(loginUrl);

            return Wrap(
                ApprovedSubject,
                $"""
                <h1 style="margin:0 0 12px;font-size:24px;line-height:1.3;">You're approved</h1>
                <p style="margin:0 0 20px;font-size:15px;line-height:1.6;color:#4b5563;">
                    Hi {name}, your technician registration on ZamMaintain has been approved.
                    You can now sign in and start receiving work.
                </p>
                <p style="margin:0 0 28px;text-align:center;">
                    <a href="{url}"
                       style="display:inline-block;padding:14px 24px;background:#1778ff;color:#ffffff;text-decoration:none;border-radius:8px;font-weight:700;">
                        Sign in
                    </a>
                </p>
                <p style="margin:0 0 12px;font-size:14px;line-height:1.6;color:#4b5563;">
                    If the button does not work, copy and paste this link into your browser:
                </p>
                <p style="margin:0;word-break:break-all;font-size:13px;line-height:1.6;color:#1778ff;">
                    <a href="{url}" style="color:#1778ff;">{url}</a>
                </p>
                """);
        }

        public static string BuildRejectedHtml(string fullName)
        {
            var name = WebUtility.HtmlEncode(fullName);

            return Wrap(
                RejectedSubject,
                $"""
                <h1 style="margin:0 0 12px;font-size:24px;line-height:1.3;">Application update</h1>
                <p style="margin:0 0 20px;font-size:15px;line-height:1.6;color:#4b5563;">
                    Hi {name}, your technician account on ZamMaintain was not approved or has been deactivated.
                    You will not be able to sign in or receive jobs while your account is inactive.
                </p>
                <p style="margin:0;font-size:13px;line-height:1.6;color:#6b7280;">
                    If you believe this was a mistake, please contact ZamMaintain support.
                </p>
                """);
        }

        public static string BuildWelcomeHtml(string fullName, string email, string password, string loginUrl)
        {
            var name = WebUtility.HtmlEncode(fullName);
            var loginEmail = WebUtility.HtmlEncode(email);
            var loginPassword = WebUtility.HtmlEncode(password);
            var url = WebUtility.HtmlEncode(loginUrl);

            return Wrap(
                WelcomeSubject,
                $"""
                <h1 style="margin:0 0 12px;font-size:24px;line-height:1.3;">Welcome to ZamMaintain</h1>
                <p style="margin:0 0 20px;font-size:15px;line-height:1.6;color:#4b5563;">
                    Hi {name}, a Super Admin created a technician account for you.
                    Use the credentials below to sign in.
                </p>
                <table role="presentation" cellspacing="0" cellpadding="0" style="width:100%;margin:0 0 24px;background:#f9fafb;border:1px solid #e5e7eb;border-radius:8px;">
                    <tr>
                        <td style="padding:16px 20px;font-size:14px;line-height:1.7;color:#111827;">
                            <div><strong>Email:</strong> {loginEmail}</div>
                            <div><strong>Password:</strong> {loginPassword}</div>
                        </td>
                    </tr>
                </table>
                <p style="margin:0 0 28px;text-align:center;">
                    <a href="{url}"
                       style="display:inline-block;padding:14px 24px;background:#1778ff;color:#ffffff;text-decoration:none;border-radius:8px;font-weight:700;">
                        Sign in
                    </a>
                </p>
                <p style="margin:0 0 12px;font-size:14px;line-height:1.6;color:#4b5563;">
                    If the button does not work, copy and paste this link into your browser:
                </p>
                <p style="margin:0 0 24px;word-break:break-all;font-size:13px;line-height:1.6;color:#1778ff;">
                    <a href="{url}" style="color:#1778ff;">{url}</a>
                </p>
                <p style="margin:0;font-size:13px;line-height:1.6;color:#6b7280;">
                    For your security, consider changing your password after you sign in.
                </p>
                """);
        }

        private static string Wrap(string title, string bodyContent) =>
            $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1" />
                <title>{WebUtility.HtmlEncode(title)}</title>
            </head>
            <body style="margin:0;padding:0;background:#f4f6f8;font-family:Arial,Helvetica,sans-serif;color:#111827;">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f4f6f8;padding:32px 16px;">
                    <tr>
                        <td align="center">
                            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;background:#ffffff;border:1px solid #d9dee7;border-radius:12px;padding:32px;">
                                <tr>
                                    <td style="text-align:center;padding-bottom:20px;">
                                        <div style="font-size:24px;font-weight:700;color:#1778ff;">ZamMaintain</div>
                                        <div style="font-size:13px;color:#6b7280;margin-top:4px;">Technician Account</div>
                                    </td>
                                </tr>
                                <tr>
                                    <td>
                                        {bodyContent}
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>
            """;
    }
}
