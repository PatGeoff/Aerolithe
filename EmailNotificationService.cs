using System.Net;
using System.Net.Mail;

namespace Aerolithe
{
    public sealed class EmailNotificationService
    {
        private readonly AppSettings _settings;

        public EmailNotificationService(AppSettings settings)
        {
            _settings = settings;
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_settings.SmtpHost)
            && _settings.SmtpPort > 0
            && !string.IsNullOrWhiteSpace(_settings.MailFrom);

        public async Task SendAsync(IEnumerable<string> recipients, string subject, string body, CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("La configuration SMTP est incomplete.");
            }

            using var message = new MailMessage
            {
                From = new MailAddress(_settings.MailFrom),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            foreach (string recipient in recipients.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                message.To.Add(recipient.Trim());
            }

            if (message.To.Count == 0)
            {
                return;
            }

            using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
            {
                EnableSsl = _settings.SmtpEnableSsl
            };

            string password = _settings.GetSmtpPassword();
            if (!string.IsNullOrWhiteSpace(_settings.SmtpUser))
            {
                client.Credentials = new NetworkCredential(_settings.SmtpUser, password);
            }

            using var registration = cancellationToken.Register(client.SendAsyncCancel);
            await client.SendMailAsync(message, cancellationToken);
        }
    }
}
