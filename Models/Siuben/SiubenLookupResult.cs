namespace Exatech_Indotel_API.Models.Siuben;

public record SiubenLookupResult(bool Exists, int? PovertyLevel = null, string? ContractNumber = null,
    bool? IsEligible = null, string? Reason = null);
