using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Exatech_Indotel_API.Models.Siuben.Phase2;
using Exatech_Indotel_API.Repositories.ClientRepository;
using Exatech_Indotel_API.Services.Clients;
using Exatech_Indotel_API.Services.Siuben;
using Exatech_Indotel_API.Services.Siuben.Phase2;
using Exatech_Indotel_API.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Exatech_Indotel_API.Controllers;

[Authorize]
[ApiController]
[Route("api/siuben")]
public class SiubenController(
    ISiubenApiProxyResolver resolver,
    SiubenLocalities localities,
    IClientRepository clients) : ControllerBase
{
    [HttpGet("localities")]
    public IActionResult GetLocalities() => Ok(localities.GetAll());

    [Authorize(Roles = "Admin")]
    [HttpPut("synchronize")]
    public async Task<IActionResult> Synchronize([FromBody] SynchronizeBeneficiaryRequest request,
        [FromServices] IClientsService clientService, [FromServices] IOptions<SiubenOptions> options)
    {
        // Always fetch the current Wispro contract; never trust a state selected by the browser.
        var details = await clientService.GetByDocumentNumber(request.DocumentNumber);
        if (details is null)
            return NotFound(new { message = "Cliente no encontrado en el repositorio de clientes." });
        if (!details.CanSynchronize)
            return Conflict(new { message = details.SynchronizationBlockedReason, clientDetails = details });

        var client = details.Client;
        if (!localities.IsSupported(client.Locality) || localities.GetPhase(client.Locality!) != SiubenPhase.Phase2)
            return BadRequest(new { message = "Esta operación requiere una localidad de fase 2 registrada en el cliente." });

        var contract = details.WisproContract;
        var number = details.ContractDeleted ? client.ContractNumber! : contract!.PublicId.ToString(CultureInfo.InvariantCulture);
        if (contract is not null && contract.PublicId <= 0)
            return BadRequest(new { message = "El contrato de Wispro no tiene un número válido." });

        var proxy = (SiubenPhase2ApiProxy)resolver.Resolve(client.Locality!);
        var enabled = contract is not null && string.Equals(contract.State, "enabled", StringComparison.OrdinalIgnoreCase);
        SiubenPhase2Response response;
        if (enabled)
        {
            if (!DateTimeOffset.TryParse(contract!.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
                return BadRequest(new { message = "El contrato de Wispro no tiene una fecha válida." });
            if (!SiubenPhoneNumber.IsValid(client.PhoneMobile, client.Phone))
                return BadRequest(new { message = "El cliente no tiene un teléfono válido registrado." });

            response = await proxy.EnableBeneficiary(new EnableBeneficiaryRequest
            {
                DocumentNumber = client.NationalIdentificationNumber.Replace("-", string.Empty).Trim(),
                PhoneNumber = SiubenPhoneNumber.Normalize(client.PhoneMobile, client.Phone),
                Provider = options.Value.Phase2.Provider,
                ContractNumber = number,
                ContractDate = date
            });
            await clients.MarkSiubenActivated(client.WisproId, number);
        }
        else
        {
            var disable = new DisableBeneficiaryRequest
            {
                DocumentNumber = client.NationalIdentificationNumber.Replace("-", string.Empty).Trim(),
                CancellationReason = request.CancellationReason,
                OtherCancellationReason = request.OtherCancellationReason
            };
            var errors = new List<ValidationResult>();
            if (!Validator.TryValidateObject(disable, new ValidationContext(disable), errors, true))
                return BadRequest(new { message = string.Join(" ", errors.Select(error => error.ErrorMessage)) });

            response = await proxy.DisableBeneficiary(disable);
            await clients.MarkSiubenDisabled(client.WisproId, number);
        }

        client.ContractNumber = number;
        client.SiubenActivated = enabled;
        client.SiubenDisabled = !enabled;
        return Ok(new { razon = response.Reason, clientDetails = details });
    }
}
