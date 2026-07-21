using System.Net;
using System.Net.Mail;

namespace CareerMatch.API.Services
{
    // Sends transactional emails such as password-reset messages.
    public class EmailService
    {
        // Gives access to the Email settings inside appsettings.json,
        // appsettings.Development.json, user secrets, or environment variables.
        private readonly IConfiguration _configuration;

        // Receives IConfiguration through dependency injection.
        public EmailService(
            IConfiguration configuration)
        {
            // Saves IConfiguration so the service can read SMTP settings later.
            _configuration = configuration;
        }

        // Sends a password-reset email to the requested recipient.
        public async Task SendPasswordResetEmailAsync(
            string recipientEmail,
            string recipientName,
            string resetLink)
        {
            // Reads the Gmail SMTP server address.
            string host =
                GetRequiredSetting(
                    "Email:SmtpHost"
                )
                .Trim();

            // Reads the SMTP port from configuration.
            int port =
                GetRequiredIntSetting(
                    "Email:SmtpPort"
                );

            // Reads the full Gmail address used to authenticate with Gmail SMTP.
            string username =
                GetRequiredSetting(
                    "Email:Username"
                )
                .Trim();

            // Reads the Google App Password.
            // Spaces are removed because Google displays App Passwords in groups.
            string password =
                GetRequiredSetting(
                    "Email:Password"
                )
                .Replace(" ", "")
                .Trim();

            // Reads the email address that recipients will see as the sender.
            string fromEmail =
                (
                    _configuration[
                        "Email:FromEmail"
                    ]
                    ?? username
                )
                .Trim();

            // Reads the friendly sender name shown in the recipient's inbox.
            string fromName =
                (
                    _configuration[
                        "Email:FromName"
                    ]
                    ?? "CareerMatch"
                )
                .Trim();

            // Reads whether SSL/TLS should be enabled.
            bool enableSsl =
                GetRequiredBoolSetting(
                    "Email:EnableSsl"
                );

            // Validates the recipient email before trying to send.
            if (string.IsNullOrWhiteSpace(
                recipientEmail))
            {
                throw new ArgumentException(
                    "Recipient email cannot be empty."
                );
            }

            // Validates the reset link before building the email.
            if (string.IsNullOrWhiteSpace(
                resetLink))
            {
                throw new ArgumentException(
                    "Reset link cannot be empty."
                );
            }

            // Validates that the Gmail App Password has the expected length.
            if (password.Length != 16)
            {
                throw new Exception(
                    "Email App Password must contain exactly 16 characters after spaces are removed."
                );
            }

            // Creates the outgoing email message.
            using var message =
                new MailMessage
                {
                    // Sets the visible sender email and display name.
                    From =
                        new MailAddress(
                            fromEmail,
                            fromName
                        ),

                    // Sets the password-reset email subject.
                    Subject =
                        "Reset your CareerMatch password",

                    // Allows the email body to contain HTML.
                    IsBodyHtml = true,

                    // Builds the HTML email body.
                    Body =
                        BuildResetEmailBody(
                            recipientName,
                            resetLink
                        )
                };

            // Adds the recipient email address.
            message.To.Add(
                recipientEmail.Trim()
            );

            // Creates the SMTP client that will connect to Gmail.
            using var smtpClient =
                new SmtpClient(
                    host,
                    port
                )
                {
                    // Prevents Windows credentials from being used automatically.
                    UseDefaultCredentials = false,

                    // Supplies the CareerMatch Gmail account and Google App Password.
                    Credentials =
                        new NetworkCredential(
                            username,
                            password
                        ),

                    // Enables TLS for the Gmail SMTP connection.
                    EnableSsl =
                        enableSsl,

                    // Forces the email to be sent through the network SMTP server.
                    DeliveryMethod =
                        SmtpDeliveryMethod.Network,

                    // Prevents emails from being written to a local pickup folder.
                    PickupDirectoryLocation =
                        string.Empty
                };

            // Sends the message asynchronously.
            await smtpClient.SendMailAsync(
                message
            );
        }

        // Reads a required string setting and throws a clear error when it is missing.
        private string GetRequiredSetting(
            string key)
        {
            // Reads the requested configuration value.
            string? value =
                _configuration[key];

            // Rejects missing or empty values.
            if (string.IsNullOrWhiteSpace(
                value))
            {
                throw new Exception(
                    $"{key} is missing from configuration."
                );
            }

            // Returns the valid configuration value.
            return value;
        }

        // Reads a required integer setting.
        private int GetRequiredIntSetting(
            string key)
        {
            // Reads the raw configuration text.
            string value =
                GetRequiredSetting(key);

            // Converts the text into an integer.
            if (!int.TryParse(
                value,
                out int result))
            {
                throw new Exception(
                    $"{key} must be a valid integer."
                );
            }

            // Returns the parsed integer.
            return result;
        }

        // Reads a required boolean setting.
        private bool GetRequiredBoolSetting(
            string key)
        {
            // Reads the raw configuration text.
            string value =
                GetRequiredSetting(key);

            // Converts the text into true or false.
            if (!bool.TryParse(
                value,
                out bool result))
            {
                throw new Exception(
                    $"{key} must be true or false."
                );
            }

            // Returns the parsed boolean.
            return result;
        }

        // Builds the HTML body used in the password-reset email.
        private static string BuildResetEmailBody(
            string recipientName,
            string resetLink)
        {
            // Uses a fallback when the recipient name is missing.
            string preparedName =
                string.IsNullOrWhiteSpace(
                    recipientName)
                    ? "CareerMatch user"
                    : recipientName.Trim();

            // HTML-encodes the name to prevent invalid HTML.
            string safeName =
                WebUtility.HtmlEncode(
                    preparedName
                );

            // HTML-encodes the reset URL before inserting it into the message.
            string safeLink =
                WebUtility.HtmlEncode(
                    resetLink.Trim()
                );

            // Returns the complete password-reset email.
            return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Reset your CareerMatch password</title>
</head>

<body style=""margin:0;padding:0;background-color:#f3f4f6;font-family:Arial,sans-serif;color:#111827;"">

    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" role=""presentation"">
        <tr>
            <td align=""center"" style=""padding:40px 16px;"">

                <table width=""100%"" cellpadding=""0"" cellspacing=""0"" role=""presentation""
                       style=""max-width:600px;background-color:#ffffff;border-radius:10px;padding:32px;"">

                    <tr>
                        <td>
                            <h1 style=""margin:0 0 20px;font-size:24px;"">
                                Reset your password
                            </h1>

                            <p style=""margin:0 0 16px;"">
                                Hello {safeName},
                            </p>

                            <p style=""margin:0 0 24px;"">
                                We received a request to reset your CareerMatch password.
                            </p>

                            <p style=""margin:0 0 24px;"">
                                <a href=""{safeLink}""
                                   style=""display:inline-block;background-color:#111827;color:#ffffff;text-decoration:none;padding:12px 20px;border-radius:6px;font-weight:bold;"">
                                    Reset Password
                                </a>
                            </p>

                            <p style=""margin:0 0 12px;font-size:14px;color:#4b5563;"">
                                This link expires in 30 minutes and can only be used once.
                            </p>

                            <p style=""margin:0;font-size:14px;color:#4b5563;"">
                                If you did not request this reset, you can safely ignore this email.
                            </p>
                        </td>
                    </tr>

                </table>

            </td>
        </tr>
    </table>

</body>
</html>";
        }
    }
}