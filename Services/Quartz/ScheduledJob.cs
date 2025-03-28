using Exatech_Indotel_API.Models.Siuben;
using Exatech_Indotel_API.Repositories.ClientRepository;
using Exatech_Indotel_API.Services.Email;
using Exatech_Indotel_API.Services.Siuben;
using Exatech_Indotel_API.Services.Wispro;
using Quartz;

namespace Exatech_Indotel_API.Services.Quartz
{
    public class ScheduledJob : IJob
    {
        private readonly IWisproApiProxy _wisproApiProxy;
        private readonly IEmailSenderService _emailSender;
        private readonly IClientRepository _clientRepository;

        public ScheduledJob(IWisproApiProxy wisproApiProxy, IEmailSenderService emailSenderService, IClientRepository clientRepository)
        {
            _wisproApiProxy = wisproApiProxy;
            _emailSender = emailSenderService;
            _clientRepository = clientRepository;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            DateTime now = DateTime.Now;

            var clients = await _clientRepository.GetClientsWithoutContract();

            var contracts = await _wisproApiProxy.GetContracsByDateRange(now, now.AddDays(1));

            clients.AsParallel().ForAll(async client =>
            {
                var contract = contracts.FirstOrDefault(x => x.ClientId == client.WisproId);

                if (contract is null)
                    return;

                await _clientRepository.AddContractNumber(client.WisproId, contract.PublicId.ToString());
            });
        }
    }
}
