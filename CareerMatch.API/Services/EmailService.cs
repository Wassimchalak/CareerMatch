
using Resend;

namespace CareerMatch.API.Services
{
    public class EmailService
    {
        private readonly IResend _resend;
        private readonly IConfiguration _configuration;

        public EmailService(
            IResend resend,
            IConfiguration configuration)
        {
            _resend = resend;
            _configuration = configuration;
        }

        public async Task SendPasswordResetEmailAsync(
            string recipientEmail,
            string recipientName,
            string resetLink)
        {
            string fromEmail =
                _configuration["Resend:FromEmail"]
                ?? throw new InvalidOperationException(
                    "Resend:FromEmail is missing."
                );

            string fromName =
                _configuration["Resend:FromName"]
                ?? "CareerMatch";

            var message = new EmailMessage
            {
                From = $"{fromName} <{fromEmail}>",
                Subject = "Reset your CareerMatch password",
                HtmlBody = BuildPasswordResetEmail(
                    recipientName,
                    resetLink
                )
            };

            message.To.Add(recipientEmail);

            await _resend.EmailSendAsync(message);
        }

        private static string BuildPasswordResetEmail(
            string recipientName,
            string resetLink)
        {
            string safeName =
                string.IsNullOrWhiteSpace(recipientName)
                    ? "there"
                    : recipientName.Trim();

            return $"""
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset="UTF-8">
                    <meta name="viewport"
                          content="width=device-width, initial-scale=1.0">
                </head>

                <body style="
                    margin:0;
                    padding:0;
                    background:#f4f4f7;
                    font-family:Arial,Helvetica,sans-serif;
                ">
                    <table width="100%"
                           cellpadding="0"
                           cellspacing="0"
                           style="padding:40px 16px;">
                        <tr>
                            <td align="center">
                                <table width="100%"
                                       cellpadding="0"
                                       cellspacing="0"
                                       style="
                                           max-width:560px;
                                           background:#ffffff;
                                           border-radius:16px;
                                           padding:40px;
                                           box-shadow:
                                               0 8px 30px rgba(0,0,0,.08);
                                       ">
                                    <tr>
                                        <td>
                                            <h1 style="
                                                margin:0 0 16px;
                                                color:#111827;
                                                font-size:28px;
                                            ">
                                                Reset your password
                                            </h1>

                                            <p style="
                                                margin:0 0 16px;
                                                color:#4b5563;
                                                font-size:16px;
                                                line-height:1.6;
                                            ">
                                                Hello {safeName},
                                            </p>

                                            <p style="
                                                margin:0 0 16px;
                                                color:#4b5563;
                                                font-size:16px;
                                                line-height:1.6;
                                            ">
                                                We received a request to reset
                                                your CareerMatch password.
                                            </p>

                                            <p style="
                                                margin:0 0 28px;
                                                color:#4b5563;
                                                font-size:16px;
                                                line-height:1.6;
                                            ">
                                                Click the button below to choose
                                                a new password.
                                            </p>

                                            <a href="{resetLink}"
                                               style="
                                                   display:inline-block;
                                                   padding:14px 24px;
                                                   background:#111827;
                                                   color:#ffffff;
                                                   text-decoration:none;
                                                   border-radius:10px;
                                                   font-size:16px;
                                                   font-weight:700;
                                               ">
                                                Reset Password
                                            </a>

                                            <p style="
                                                margin:28px 0 8px;
                                                color:#6b7280;
                                                font-size:14px;
                                                line-height:1.6;
                                            ">
                                                If the button does not work,
                                                copy and paste this link into
                                                your browser:
                                            </p>

                                            <p style="
                                                margin:0;
                                                color:#6d5dfc;
                                                font-size:13px;
                                                word-break:break-all;
                                            ">
                                                {resetLink}
                                            </p>

                                            <p style="
                                                margin:28px 0 0;
                                                color:#9ca3af;
                                                font-size:13px;
                                                line-height:1.6;
                                            ">
                                                This link expires in 30 minutes.
                                                If you did not request this
                                                password reset, you can ignore
                                                this email.
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
}
