using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Exatech_Indotel_API.Models.Client;
using Exatech_Indotel_API.Models.Clients;
using Exatech_Indotel_API.Models.Email;
using Exatech_Indotel_API.Models.Wispro;
using Exatech_Indotel_API.Services.Siuben;
using Exatech_Indotel_API.Services.Wispro;
using System.Text.Json;

namespace Exatech_Indotel_API.Services.Clients
{
    public class ClientsService : IClientsService
    {
        private readonly IWisproApiProxy _wisproApiProxy;
        private readonly ISiubenApiProxy _siubenApiProxy;

       // private readonly static string _htmlTemplate;

        //private readonly EventHubProducerClient _eventproducerClient;

       // static ClientsService() => _htmlTemplate = File.ReadAllText("Templates/ClientCreated.html");
        
        public ClientsService(ISiubenApiProxy siubenApiProxy, IWisproApiProxy wisproApiProxy /*, EventHubProducerClient eventHubProducerClient*/)
        {
            _wisproApiProxy = wisproApiProxy;
            _siubenApiProxy = siubenApiProxy;
           // _eventproducerClient = eventHubProducerClient;
        }

        public async Task<CheckClientResponse> CheckClient(string documentNumber, string? phoneNumber, string? email)
        {
            var result = new CheckClientResponse();

            var wisproTask = _wisproApiProxy.GetClient(documentNumber, phoneNumber, email);

            var siubenTask = _siubenApiProxy.GetContract(documentNumber);

            await Task.WhenAll(wisproTask, siubenTask);

            result.ClientId = wisproTask.Result?.Id ?? null;
            result.ContractNumber = siubenTask.Result?.ContractNumber ?? null;
            result.ExistsInWispro = !string.IsNullOrEmpty(wisproTask.Result?.Id);
            result.ExistsInSiuben = siubenTask.Result?.PobertyLevel is not null;

            return result;
        }

        public async Task<string> CreateClient(ClientCreateDto client)
        {
            var wisproClient = new WisproClient
            {
                Name = client.Name,
                Email = client.Email,
                Street = client.Street,
                Number = client.Number,
                City = client.City,
                Phone = client.Phone,
                PhoneMobile = client.PhoneMobile,
                State = client.State,
                NationalIdentificationNumber = client.NationalIdentificationNumber
            };

            var response = await _wisproApiProxy.CreateClient(wisproClient);

            //var notification = new EmailNotification
            //{
            //    To = client.Email,
            //    Subject = "Cliente Creado",
            //    Body = _htmlTemplate.Replace("{{clientName}}", client.Name),
            //};

            //_ = Task.Run(async () => {
            //    using EventDataBatch eventBatch = await _eventproducerClient.CreateBatchAsync();

            //    EventData eventData = new EventData
            //    {
            //        CorrelationId = Guid.NewGuid().ToString(),
            //        EventBody = BinaryData.FromString(JsonSerializer.Serialize(notification)),
            //        MessageId = Guid.NewGuid().ToString(),
            //        ContentType = "application/json"
            //    };

            //    if (eventBatch?.TryAdd(eventData) ?? false)
            //        await _eventproducerClient.SendAsync(eventBatch);
            //});

            return response?.Id ?? string.Empty;   
        }
    }
}
