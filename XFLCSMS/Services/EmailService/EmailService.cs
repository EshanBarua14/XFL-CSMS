using MailKit.Security;
using MimeKit.Text;
using MimeKit;
using MailKit.Net.Smtp;
using XFLCSMS.Models.Email;

namespace XFLCSMS.EmailService
{
    public class EmailService:IEmailServices
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public void SendEmail(EmailDto request)
        {
            if (string.IsNullOrWhiteSpace(_config["EmailHost"]) || string.IsNullOrWhiteSpace(_config["EmailUsername"]) || string.IsNullOrWhiteSpace(_config["EmailPassword"]))
            {
                throw new InvalidOperationException("E-mail is not configured: set EmailHost, EmailUsername and EmailPassword (appsettings.Local.json or environment variables).");
            }

            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(_config.GetSection("EmailUsername").Value));
            email.To.Add(MailboxAddress.Parse(request.To));
            email.Subject = request.Subject;
            email.Body = new TextPart(TextFormat.Html) { Text = request.Body };

            using var smtp = new SmtpClient();
            smtp.Timeout = 20000; // do not let a dead mail server hang the web request for minutes
            smtp.Connect(_config.GetSection("EmailHost").Value, _config.GetValue("EmailPort", 587), SecureSocketOptions.StartTls);
            smtp.Authenticate(_config.GetSection("EmailUsername").Value, _config.GetSection("EmailPassword").Value);
            smtp.Send(email);
            smtp.Disconnect(true);
        }
    }
}
