
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Exatech_Indotel_API.Entities;
using Exatech_Indotel_API.Models.Email;
using Exatech_Indotel_API.Repositories.EmailTemplateRepository;
using Exatech_Indotel_API.Utilities;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Exatech_Indotel_API.Services.Email
{
    public class EmailSenderService : IEmailSenderService
    {
        private readonly AppOptions _appOptions;
        private readonly EventHubProducerClient _eventproducerClient;
        private readonly IEmailTemplateRepository _emailTemplateRepository;

        public EmailSenderService(IOptions<AppOptions> options, EventHubProducerClient eventHubProducerClient, IEmailTemplateRepository emailtemplateRepository)
        {
            _appOptions = options.Value;
            _eventproducerClient = eventHubProducerClient;
            _emailTemplateRepository = emailtemplateRepository;
        }

        public async Task SendEmail(Client client, EventType eventType)
        {
            (string templateName, string emailSubject) = eventType switch {
                EventType.ClientCreation => (_appOptions.ClientCreationTemplate, "Cliente creado en Wispro"),
                EventType.ContractUpdateRequest => (_appOptions.ClientUpdateRequestTemplate, "Cliente solicita cambio de contrato"),
                _ => (string.Empty, string.Empty)
            };

            var template = await _emailTemplateRepository.Get(templateName);

            var notification = new EmailNotification
            {
                Body = template.FillTemplate(client),
                Subject = emailSubject,
                To = _appOptions.EmailReciver,
                EventType = eventType
            };

            using EventDataBatch eventBatch = await _eventproducerClient.CreateBatchAsync();

            EventData eventData = new EventData
            {
                CorrelationId = Guid.NewGuid().ToString(),
                EventBody = BinaryData.FromString(JsonSerializer.Serialize(notification)),
                MessageId = Guid.NewGuid().ToString(),
                ContentType = "application/json"
            };

            if (eventBatch?.TryAdd(eventData) ?? false)
                await _eventproducerClient.SendAsync(eventBatch);
        }
    }
}
