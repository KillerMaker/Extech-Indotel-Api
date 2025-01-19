
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;
using Exatech_Indotel_API.Models.Email;
using Exatech_Indotel_API.Utilities;
using Microsoft.Extensions.Options;
using System.Net;
//using System.Net.Mail;
using System.Text;
using System.Text.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Exatech_Indotel_API.Services.Email
{
    public class EmailProcessorBackgroundService : BackgroundService
    {
        private readonly AppOptions _appOptions;
        private readonly EventProcessorClient _eventProcessorClient;

        public EmailProcessorBackgroundService(IOptions<AppOptions> options, EventProcessorClient eventProcessorClient)
        {
            _appOptions = options.Value;
            _eventProcessorClient = eventProcessorClient;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _eventProcessorClient.ProcessEventAsync += ProcessEventHandler;
            _eventProcessorClient.ProcessErrorAsync += ProcessErrorHandler;

            await _eventProcessorClient.StartProcessingAsync(stoppingToken);
        }

        private async Task ProcessErrorHandler(ProcessErrorEventArgs args)
        {
            Console.WriteLine(args.Exception.Message);
        }
        private async Task ProcessEventHandler(ProcessEventArgs eventArgs)
        {
            var data = Encoding.UTF8.GetString(eventArgs.Data.Body.ToArray());

            var notification = JsonSerializer.Deserialize<EmailNotification>(data) ?? throw new Exception();

            await eventArgs.UpdateCheckpointAsync(eventArgs.CancellationToken);

            string sender = _appOptions.EmailSender;
            string password = _appOptions.EmailPassword;

            var mail = new MimeMessage();

            mail.Sender = new MailboxAddress("Exatech", sender);
            mail.From.Add(MailboxAddress.Parse(sender));
            mail.Subject = notification.Subject;

            mail.To.Add(MailboxAddress.Parse(notification.To));
            
            foreach(var copy in notification.CC ?? Array.Empty<string>())
                mail.Cc.Add(MailboxAddress.Parse(copy));

            var builder = new BodyBuilder
            {
                HtmlBody = notification.Body
            };

            mail.Body = builder.ToMessageBody();

            using var smtpClient = new SmtpClient();

            await smtpClient.ConnectAsync(
                _appOptions.EmailHost, 
                _appOptions.EmailPort,
                SecureSocketOptions.SslOnConnect,
                eventArgs.CancellationToken);

            await smtpClient.AuthenticateAsync(sender, password, eventArgs.CancellationToken);

            var s = await smtpClient.SendAsync(mail, eventArgs.CancellationToken);

            await smtpClient.DisconnectAsync(true, eventArgs.CancellationToken);
        }

        
    }
}
