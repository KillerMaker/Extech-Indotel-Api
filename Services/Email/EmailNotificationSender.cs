
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;
using Exatech_Indotel_API.Models.Email;
using Exatech_Indotel_API.Utilities;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;

namespace Exatech_Indotel_API.Services.Email
{
    public class EmailNotificationSender : BackgroundService
    {
        private readonly AppOptions _appOptions;
        private readonly EventProcessorClient _eventProcessorClient;

        public EmailNotificationSender(IOptions<AppOptions> options, EventProcessorClient eventProcessorClient)
        {
            _appOptions = options.Value;
            _eventProcessorClient = eventProcessorClient;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _eventProcessorClient.ProcessEventAsync += ProcessEventHandler;

            await _eventProcessorClient.StartProcessingAsync(stoppingToken);
        }

        private async Task ProcessEventHandler(ProcessEventArgs eventArgs)
        {
            var data = Encoding.UTF8.GetString(eventArgs.Data.Body.ToArray());

            var notification = JsonSerializer.Deserialize<EmailNotification>(data) ?? throw new Exception();

            await eventArgs.UpdateCheckpointAsync(eventArgs.CancellationToken);

            string sender = _appOptions.EmailSender;
            string password = _appOptions.EmailPassword;

            var client = new SmtpClient(_appOptions.EmailHost, _appOptions.EmailPort)
            {
                Credentials = new NetworkCredential(sender, password),
                EnableSsl = true
            };

            var message = new MailMessage(
                sender,
                notification.To,
                notification.Subject,
                notification.Body
                );

            foreach(var copy in notification.CC ?? Array.Empty<string>())
                message.CC.Add(copy);

            await client.SendMailAsync(message);
        }

        
    }
}
