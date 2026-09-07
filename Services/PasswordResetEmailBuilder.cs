using System.Net;

namespace CloudMVCApplication.Services
{
    public static class PasswordResetEmailBuilder
    {
        public const string Subject = "Reset your ZamMaintain password";

        public static string BuildHtml(string resetUrl) =>
            $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1" />
                <title>{Subject}</title>
            </head>
            <body style="margin:0;padding:0;background:#f4f6f8;font-family:Arial,Helvetica,sans-serif;color:#111827;">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f4f6f8;padding:32px 16px;">
                    <tr>
                        <td align="center">
                            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;background:#ffffff;border:1px solid #d9dee7;border-radius:12px;padding:32px;">
                                <tr>
                                    <td style="text-align:center;padding-bottom:20px;">
                                        <div style="font-size:24px;font-weight:700;color:#1778ff;">ZamMaintain</div>
                                        <div style="font-size:13px;color:#6b7280;margin-top:4px;">Enterprise Security</div>
                                    </td>
                                </tr>
                                <tr>
                                    <td>
                                        <h1 style="margin:0 0 12px;font-size:24px;line-height:1.3;">Reset your password</h1>
                                        <p style="margin:0 0 20px;font-size:15px;line-height:1.6;color:#4b5563;">
                                            We received a request to reset the password for your ZamMaintain account.
                                            Click the button below to choose a new password.
                                        </p>
                                        <p style="margin:0 0 28px;text-align:center;">
                                            <a href="{WebUtility.HtmlEncode(resetUrl)}"
                                               style="display:inline-block;padding:14px 24px;background:#1778ff;color:#ffffff;text-decoration:none;border-radius:8px;font-weight:700;">
                                                Reset Password
                                            </a>
                                        </p>
                                        <p style="margin:0 0 12px;font-size:14px;line-height:1.6;color:#4b5563;">
                                            If the button does not work, copy and paste this link into your browser:
                                        </p>
                                        <p style="margin:0 0 24px;word-break:break-all;font-size:13px;line-height:1.6;color:#1778ff;">
                                            <a href="{WebUtility.HtmlEncode(resetUrl)}" style="color:#1778ff;">{WebUtility.HtmlEncode(resetUrl)}</a>
                                        </p>
                                        <p style="margin:0;font-size:13px;line-height:1.6;color:#6b7280;">
                                            If you did not request a password reset, you can safely ignore this email.
                                            Your password will not change unless you use the link above.
                                        </p>
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
