using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Exatech_Indotel_API.Entities;
using Exatech_Indotel_API.Models.Client;
using Exatech_Indotel_API.Models.Clients;
using Exatech_Indotel_API.Models.Email;
using Exatech_Indotel_API.Models.Wispro;
using Exatech_Indotel_API.Repositories.ClientRepository;
using Exatech_Indotel_API.Services.Email;
using Exatech_Indotel_API.Services.Siuben;
using Exatech_Indotel_API.Services.Wispro;
using Exatech_Indotel_API.Utilities.Extensions;
using System.Text.Json;

namespace Exatech_Indotel_API.Services.Clients
{
    public class ClientsService : IClientsService
    {
        private readonly IWisproApiProxy _wisproApiProxy;
        private readonly ISiubenApiProxy _siubenApiProxy;
        private readonly IClientRepository _clientRepository;
        private readonly IHttpContextAccessor _contextAccesor;
        private readonly IEmailSenderService _emailSender;

        public ClientsService(
            ISiubenApiProxy siubenApiProxy, 
            IWisproApiProxy wisproApiProxy, 
            IClientRepository clientRepository, 
            IHttpContextAccessor contextAccessor,
            IEmailSenderService emailSenderService)
        {
            _wisproApiProxy = wisproApiProxy;
            _siubenApiProxy = siubenApiProxy;
            _clientRepository = clientRepository;
            _contextAccesor = contextAccessor;
            _emailSender = emailSenderService;
        }

        public async Task<CheckClientResponse> CheckClient(string documentNumber, string? phoneNumber, string? email)
        {
            var wisproTask = _wisproApiProxy.GetClient(documentNumber, phoneNumber, email);

            var siubenTask = _siubenApiProxy.GetContract(documentNumber);

            await Task.WhenAll(wisproTask, siubenTask);

            return new CheckClientResponse {
                ClientId = wisproTask.Result?.Id ?? null,
                ContractNumber = siubenTask.Result?.ContractNumber ?? null,
                ExistsInWispro = !string.IsNullOrEmpty(wisproTask.Result?.Id),
                ExistsInSiuben = siubenTask.Result?.PobertyLevel is not null
            };
        }

        public async Task<CreateClientResponse?> CreateClient(CreateClientRequest clientDto)
        {
            if (await _clientRepository.Exists(clientDto.NationalIdentificationNumber))
                return null;

            var wisproClient = clientDto.ToWisproClient();

            var userId = _contextAccesor.HttpContext?.User.Claims
                .First(claim => claim.Type.Equals("userId")).Value ?? throw new Exception();
            
            var wisproResponse = await _wisproApiProxy.CreateClient(wisproClient);

            if (wisproResponse?.Id is null || wisproResponse?.PublicId is null)
                throw new AggregateException("Failed to create Client in Wispro");

            var clientEntity = clientDto.ToClient(wisproResponse.Id, userId, wisproResponse.PublicId.Value);

            await _clientRepository.Create(clientEntity);

            await _emailSender.SendEmail(clientEntity, EventType.ClientCreation);

            return clientEntity.ToCreateClientResponse();   
        }
    }
}
