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
        private readonly SiubenLocalities _localities;
        private readonly IWisproApiProxy _wisproApiProxy;
        private readonly ISiubenApiProxyResolver _siubenResolver;
        private readonly IClientRepository _clientRepository;
        private readonly IHttpContextAccessor _contextAccesor;
        private readonly IEmailSenderService _emailSender;
        private readonly ILogger<ClientsService> _logger;

        public ClientsService(
            ISiubenApiProxyResolver siubenResolver,
            IWisproApiProxy wisproApiProxy, 
            IClientRepository clientRepository, 
            IHttpContextAccessor contextAccessor,
            IEmailSenderService emailSenderService,
            SiubenLocalities localities,
            ILogger<ClientsService>? logger = null)
        {
            _localities = localities;
            _wisproApiProxy = wisproApiProxy;
            _siubenResolver = siubenResolver;
            _clientRepository = clientRepository;
            _contextAccesor = contextAccessor;
            _emailSender = emailSenderService;
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ClientsService>.Instance;
        }

        public async Task<ClientDetailsResponse?> GetByDocumentNumber(string documentNumber)
        {
            var document = documentNumber.Replace("-", string.Empty).Trim();
            var client = await _clientRepository.GetByDocumentNumber(document);
            if (client is null)
                return null;

            var contract = string.IsNullOrWhiteSpace(client.WisproId) ? null :
                await _wisproApiProxy.GetContractForClient(document, client.WisproId, client.ContractNumber);

            return new ClientDetailsResponse { Client = client, WisproContract = contract };
        }

        public async Task<CheckClientResponse> CheckClient(string documentNumber, string? phoneNumber, string? email, string locality)
        {
            var siuben = _siubenResolver.Resolve(locality);
            var wisproTask = _wisproApiProxy.GetClient(documentNumber);
            var siubenTask = siuben.GetContract(documentNumber);

            await Task.WhenAll(wisproTask, siubenTask);
            var beneficiary = await siubenTask;
            var phase = _localities.GetPhase(locality);

            return new CheckClientResponse {
                ClientId = wisproTask.Result?.Id ?? null,
                ContractNumber = beneficiary.ContractNumber,
                ExistsInWispro = !string.IsNullOrEmpty(wisproTask.Result?.Id),
                ExistsInSiuben = beneficiary.Exists,
                PobertyLevel = phase == SiubenPhase.Phase1 ? beneficiary.PovertyLevel ?? 0 : null,
                SiubenPhase = (int)phase,
                IsEligible = beneficiary.IsEligible,
                Reason = beneficiary.Reason
            };
        }

        public async Task<CreateClientResponse?> CreateClient(CreateClientRequest clientDto)
        {
            clientDto.Locality = _localities.GetName(clientDto.Locality);

            if (await TelemetryExtensions.RunStage("client.check_duplicate", () => _clientRepository.Exists(clientDto.NationalIdentificationNumber), _logger))
                return null;

            var wisproClient = clientDto.ToWisproClient();

            var userId = _contextAccesor.HttpContext?.User.Claims
                .First(claim => claim.Type.Equals("userId")).Value ?? throw new Exception();
            
            var wisproResponse = await TelemetryExtensions.RunStage("client.create_wispro", () => _wisproApiProxy.CreateClient(wisproClient), _logger);

            if (string.IsNullOrWhiteSpace(wisproResponse?.Id) || wisproResponse.PublicId is null)
                throw new AggregateException("Failed to create Client in Wispro");

            var clientEntity = clientDto.ToClient(wisproResponse.Id, userId, wisproResponse.PublicId.Value);

            await TelemetryExtensions.RunStage("client.save_sql", () => _clientRepository.Create(clientEntity), _logger);

            await TelemetryExtensions.RunStage("client.prepare_email", () => _emailSender.SendEmail(clientEntity, EventType.ClientCreation), _logger);

            return clientEntity.ToCreateClientResponse();
        }
    }
}
