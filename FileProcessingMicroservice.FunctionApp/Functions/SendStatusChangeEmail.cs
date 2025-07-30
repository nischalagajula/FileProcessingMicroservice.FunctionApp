using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Threading.Tasks;

namespace EmailMicroservice.Functions
{
    public class SendStatusChangeEmailFunction
    {
        private readonly ILogger<SendStatusChangeEmailFunction> _logger;

        public SendStatusChangeEmailFunction(ILogger<SendStatusChangeEmailFunction> logger)
        {
            _logger = logger;
        }

        [Function("SendStatusChangeEmailFunction")]
        public async Task RunAsync(
            [QueueTrigger("%QueueName%", Connection = "AzureWebJobsStorage")] string queueMessage)
        {
            _logger.LogInformation($"Queue trigger received message: {queueMessage}");

            // Outlook email
            await SendEmail(
                toEmail: "recipient@outlook.com",
                subject: "Status Changed - Outlook",
                body: $"Queue Message: {queueMessage}",
                smtpHost: "smtp.office365.com",
                smtpPort: 587,
                fromEmail: "raghav07.official@outlook.com",
                password: "oxpylwrqzgvgmbdm"
            );

            // Gmail email
            await SendEmail(
                toEmail: "recipient@gmail.com",
                subject: "Status Changed - Gmail",
                body: $"Queue Message: {queueMessage}",
                smtpHost: "smtp.gmail.com",
                smtpPort: 587,
                fromEmail: "ksraghavkashyap@gmail.com",
                password: "vprz yosc ajgg iwqs"
            );
        }

        private async Task SendEmail(string toEmail, string subject, string body,
            string smtpHost, int smtpPort, string fromEmail, string password)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Status Notifier", fromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();
            await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(fromEmail, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}
