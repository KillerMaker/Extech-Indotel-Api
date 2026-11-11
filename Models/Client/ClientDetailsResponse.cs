using Exatech_Indotel_API.Models.Wispro;

namespace Exatech_Indotel_API.Models.Client;

public class ClientDetailsResponse
{
    public required Entities.Client Client { get; init; }
    public WisproContract? WisproContract { get; init; }
    public bool AlreadySynchronized => WisproContract is not null &&
        ((string.Equals(WisproContract.State, "enabled", StringComparison.OrdinalIgnoreCase) && Client.SiubenActivated && !Client.SiubenDisabled) ||
         (string.Equals(WisproContract.State, "disabled", StringComparison.OrdinalIgnoreCase) && Client.SiubenDisabled && !Client.SiubenActivated));
    public bool CanSynchronize => !AlreadySynchronized && WisproContract is not null &&
        (string.Equals(WisproContract.State, "enabled", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(WisproContract.State, "disabled", StringComparison.OrdinalIgnoreCase));
    public string? SynchronizationBlockedReason => AlreadySynchronized
        ? "Ya está sincronizado con Wispro."
        : WisproContract is null
        ? "El cliente no tiene un contrato en Wispro."
        : CanSynchronize ? null : "El estado del contrato en Wispro no permite sincronizar el beneficio.";
}
