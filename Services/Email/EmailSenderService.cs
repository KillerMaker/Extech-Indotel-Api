using Confluent.Kafka;
using Exatech_Indotel_API.Entities;
using Exatech_Indotel_API.Models.Email;
using Exatech_Indotel_API.Repositories.EmailTemplateRepository;
using Exatech_Indotel_API.Utilities;
using Exatech_Indotel_API.Utilities.Extensions;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Exatech_Indotel_API.Services.Email
{
    public class EmailSenderService : IEmailSenderService
    {
        private readonly AppOptions _appOptions;
        private readonly IProducer<Null, string> _producer;
        private readonly ILogger<EmailSenderService> _logger;
        private readonly EmailQueueOptions _queueOptions;
        private readonly IEmailTemplateRepository _emailTemplateRepository;

        public EmailSenderService(IOptions<AppOptions> options, IProducer<Null, string> producer, IOptions<EmailQueueOptions> queueOptions, IEmailTemplateRepository emailtemplateRepository, ILogger<EmailSenderService> logger)
        {
            _appOptions = options.Value;
            _producer = producer;
            _logger = logger;
            _queueOptions = queueOptions.Value;
            _emailTemplateRepository = emailtemplateRepository;
        }

        public async Task SendEmail(Client client, EventType eventType, IEnumerable<string>? copies = null)
        {
            await SendEmail(client, eventType, ExtensionMethods.FillTemplate, copies);
        }

        public async Task SendEmail<T>(T data, EventType eventType, Func<EmailTemplate, T, string> fillTemplate, IEnumerable<string>? copies = null)
        {
            (string templateName, string emailSubject) = eventType switch
            {
                EventType.ClientCreation => (_appOptions.ClientCreationTemplate, "Cliente Superate creado en Wispro"),
                EventType.ContractUpdateRequest => (_appOptions.ClientUpdateRequestTemplate, "Cliente solicita cambio de contrato"),
                EventType.UserCreation => (_appOptions.UserCreationTemplate, "Usuario creado en el portal Exatech"),
                _ => (string.Empty, string.Empty)
            };

            var template = await _emailTemplateRepository.Get(templateName);


            var notification = new EmailNotification
            {
                Body = fillTemplate.Invoke(template, data),
                Subject = emailSubject,
                To = _appOptions.EmailReciver,
                System = _queueOptions.System,
                CC = copies?.ToArray() ?? Array.Empty<string>()
            };

            var payload = JsonSerializer.Serialize(notification, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            _ = PublishToKafkaAsync(payload);
        }

        private async Task PublishToKafkaAsync(string payload)
        {
            using var activity = TelemetryExtensions.Activities.StartActivity("email.publish_kafka", System.Diagnostics.ActivityKind.Producer);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await _producer.ProduceAsync(_queueOptions.Topic, new Message<Null, string> { Value = payload });
                _logger.LogInformation("Kafka email publication completed in {ElapsedMs} ms", timer.Elapsed.TotalMilliseconds);
            }
            catch (Exception exception)
            {
                // Do not log the exception's Kafka message: it contains client contact details.
                activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error);
                activity?.SetTag("error.type", exception.GetType().FullName);
                _logger.LogError("Background Kafka email publication failed ({FailureType}).", exception.GetType().Name);
            }
        }
    }
}
