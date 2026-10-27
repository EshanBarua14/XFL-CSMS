using MailKit.Security;
using MimeKit.Text;
using MimeKit;
using MailKit.Net.Smtp;
using XFLCSMS.Models.Email;
using XFLCSMS.Services.Notifications;

namespace XFLCSMS.EmailService
{
    public class EmailService:IEmailServices
    {
        private readonly IConfiguration _config;
        private readonly NotificationSettingsStore _settings;

        public EmailService(IConfiguration config, NotificationSettingsStore settings)
        {
            _config = config;
            _settings = settings;
        }

        /// <summary>SMTP from Admin > Notifications > Providers, else EmailHost / EmailUsername / EmailPassword.</summary>
        private string? Smtp(string key, string legacy) => _settings.Get(key) is { Length: > 0 } v ? v : _config[legacy];

        public void SendEmail(EmailDto request)
        {
            var host = Smtp(SettingKeys.SmtpHost, "EmailHost");
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new InvalidOperationException("E-mail is not configured: set the SMTP server under Admin > Notifications > Providers (or EmailHost / EmailUsername / EmailPassword in appsettings.Local.json or environment variables).");
            }

            var user = Smtp(SettingKeys.SmtpUser, "EmailUsername");
            var from = _settings.Get(SettingKeys.SmtpFrom) is { Length: > 0 } f ? f : user;

            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(_settings.Get(SettingKeys.SmtpFromName) ?? "XFL CSMS", from));
            email.To.Add(MailboxAddress.Parse(request.To));
            email.Subject = request.Subject;
            email.Body = new TextPart(TextFormat.Html) { Text = request.Body };

            var security = (_settings.Get(SettingKeys.SmtpSecurity) ?? "StartTls") switch
            {
                "SslOnConnect" => SecureSocketOptions.SslOnConnect,
                "None" => SecureSocketOptions.None,
                "Auto" => SecureSocketOptions.Auto,
                _ => SecureSocketOptions.StartTls
            };

            using var smtp = new SmtpClient();
            smtp.Timeout = 20000; // do not let a dead mail server hang the web request for minutes
            smtp.Connect(host, _settings.GetInt(SettingKeys.SmtpPort, _config.GetValue("EmailPort", 587)), security);
            if (!string.IsNullOrWhiteSpace(user))
            {
                smtp.Authenticate(user, Smtp(SettingKeys.SmtpPassword, "EmailPassword") ?? string.Empty);
            }

            smtp.Send(email);
            smtp.Disconnect(true);
        }
    }
}
