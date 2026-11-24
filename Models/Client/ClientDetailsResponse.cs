using Exatech_Indotel_API.Models.Wispro;

namespace Exatech_Indotel_API.Models.Client;

public class ClientDetailsResponse
{
    public required Entities.Client Client { get; init; }
    public WisproContract? WisproContract { get; init; }
    public bool ContractDeleted => WisproContract is null && !string.IsNullOrWhiteSpace(Client.WisproId) &&
        !string.IsNullOrWhiteSpace(Client.ContractNumber);
    private bool ContractEnabled => string.Equals(WisproContract?.State, "enabled", StringComparison.OrdinalIgnoreCase);
    private bool ContractDisabled => ContractDeleted || string.Equals(WisproContract?.State, "disabled", StringComparison.OrdinalIgnoreCase);
    public bool AlreadySynchronized =>
        (ContractEnabled && Client.SiubenActivated && !Client.SiubenDisabled) ||
        (ContractDisabled && Client.SiubenDisabled && !Client.SiubenActivated);
    public bool CanSynchronize => !AlreadySynchronized && (ContractEnabled || ContractDisabled);
    public string? SynchronizationBlockedReason => AlreadySynchronized
        ? "Ya está sincronizado con Wispro."
        : WisproContract is null && !ContractDeleted
        ? "El cliente no tiene un contrato en Wispro."
        : CanSynchronize ? null : "El estado del contrato en Wispro no permite sincronizar el beneficio.";
}
