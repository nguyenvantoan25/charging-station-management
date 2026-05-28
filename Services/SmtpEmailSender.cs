using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace tramsac99.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly SmtpEmailSettings _settings;

        public SmtpEmailSender(IOptions<SmtpEmailSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_settings.Host) ||
                string.IsNullOrWhiteSpace(_settings.SenderEmail))
            {
                throw new InvalidOperationException(
                    "SMTP chưa được cấu hình. Hãy cập nhật mục EmailSettings trong appsettings.json trước khi dùng quên mật khẩu.");
            }

            using var message = new MailMessage();
            message.From = new MailAddress(_settings.SenderEmail, _settings.SenderName);
            message.To.Add(new MailAddress(toEmail));
            message.Subject = subject;
            message.Body = htmlBody;
            message.IsBodyHtml = true;

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };

            if (!string.IsNullOrWhiteSpace(_settings.Username))
            {
                client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await client.SendMailAsync(message);
        }
    }
}