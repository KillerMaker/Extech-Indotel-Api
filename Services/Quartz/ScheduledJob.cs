using System.Globalization;
using System.Net;
using Exatech_Indotel_API.Entities;
using Exatech_Indotel_API.Models.Siuben.Phase2;
using Exatech_Indotel_API.Models.Wispro;
using Exatech_Indotel_API.Repositories.ClientRepository;
using Exatech_Indotel_API.Services.Siuben;
using Exatech_Indotel_API.Services.Siuben.Phase2;
using Exatech_Indotel_API.Services.Wispro;
using Exatech_Indotel_API.Utilities;
using Microsoft.Extensions.Options;
using Quartz;

namespace Exatech_Indotel_API.Services.Quartz;

[DisallowConcurrentExecution]
public class ScheduledJob(
    IWisproApiProxy wispro,
    IClientRepository clients,
    ISiubenApiProxyResolver resolver,
    SiubenLocalities localities,
    IOptions<SiubenOptions> options,
    ILogger<ScheduledJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var allClients = await clients.GetClientsForContractSynchronization();
        foreach (var client in allClients)
        {
            var isPhase2 = localities.IsSupported(client.Locality) &&
                localities.GetPhase(client.Locality!) == SiubenPhase.Phase2;

            try
            {
                var contract = await wispro.GetContractForClient(client.NationalIdentificationNumber, client.WisproId, client.ContractNumber);

                if (contract is null)
                {
                    if (isPhase2 && !string.IsNullOrWhiteSpace(client.WisproId) && !string.IsNullOrWhiteSpace(client.ContractNumber))
                        await DisableBeneficiary(client, client.ContractNumber);

                    continue;
                }

                var contractNumber = contract.PublicId.ToString(CultureInfo.InvariantCulture);

                if (isPhase2 && string.Equals(contract.State, "disabled", StringComparison.OrdinalIgnoreCase))
                    await DisableBeneficiary(client, contractNumber);
                
                else if (isPhase2 && string.Equals(contract.State, "enabled", StringComparison.OrdinalIgnoreCase) && !client.SiubenActivated)
                    await EnableBeneficiary(client, contract, contractNumber);
                
                else
                    if (client.ContractNumber != contractNumber)
                        await clients.AddContractNumber(client.WisproId, contractNumber);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Contract synchronization failed for Wispro client {WisproId}; it remains pending.", client.WisproId);
            }
        }
    }

    private async Task EnableBeneficiary(Client client, WisproContract contract, string contractNumber)
    {
        if (contract.PublicId <= 0 || !DateTimeOffset.TryParse(contract.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var contractDate))
            throw new InvalidOperationException("Wispro contract must have a valid public number and creation date to enable the Siuben beneficiary.");

        var proxy = (SiubenPhase2ApiProxy)resolver.Resolve(client.Locality!);

        await proxy.EnableBeneficiary(new EnableBeneficiaryRequest
        {
            DocumentNumber = client.NationalIdentificationNumber.Replace("-", string.Empty),
            PhoneNumber = SiubenPhoneNumber.Normalize(client.PhoneMobile, client.Phone),
            Provider = options.Value.Phase2.Provider,
            ContractNumber = contractNumber,
            ContractDate = contractDate
        });
        
        await clients.MarkSiubenActivated(client.WisproId, contractNumber);
    }

    private async Task DisableBeneficiary(Client client, string contractNumber)
    {
        if (client.SiubenDisabled) return;

        var proxy = (SiubenPhase2ApiProxy)resolver.Resolve(client.Locality!);

        try
        {
            await proxy.DisableBeneficiary(new DisableBeneficiaryRequest
            {
                DocumentNumber = client.NationalIdentificationNumber.Replace("-", string.Empty),
                CancellationReason = options.Value.Phase2.DisabledContractReason,
                OtherCancellationReason = options.Value.Phase2.DisabledContractOtherReason
            });
        }
        catch (SiubenApiException exception) when (exception.StatusCode == HttpStatusCode.BadRequest && 
            string.Equals(exception.Message, "Hogar ya est\u00e1 de baja", StringComparison.OrdinalIgnoreCase)){}

        await clients.MarkSiubenDisabled(client.WisproId, contractNumber);
    }
}
