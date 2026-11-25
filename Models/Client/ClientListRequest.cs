using System.ComponentModel.DataAnnotations;
using Exatech_Indotel_API.Entities;

namespace Exatech_Indotel_API.Models.Client;

public class ClientListRequest
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 25;
    [MaxLength(150)] public string? Search { get; set; }
    [MaxLength(100)] public string? Locality { get; set; }
    [RegularExpression("^(enabled|disabled|pending|inconsistent)$")] public string? Status { get; set; }
    public bool? HasContract { get; set; }

    public ClientListResponse Apply(IEnumerable<Entities.Client> clients)
    {
        var rows = clients.Where(c =>
            (string.IsNullOrWhiteSpace(Search) || new[] { c.Name, c.Email, c.ContractNumber, c.WisproId }
                .Any(v => v?.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase) == true) ||
                c.NationalIdentificationNumber.Replace("-", "").Contains(Search.Trim().Replace("-", ""), StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(Locality) || string.Equals(c.Locality, Locality, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(Status) || GetStatus(c) == Status) &&
            (HasContract is null || HasContract == !string.IsNullOrWhiteSpace(c.ContractNumber))).OrderByDescending(c => c.ClientId).ToArray();
        return new(rows.Skip((int)Math.Min(rows.Length, ((long)Page - 1) * PageSize)).Take(PageSize).ToArray(), rows.Length, Page, PageSize);
    }

    public static string GetStatus(Entities.Client c) => c.SiubenActivated && c.SiubenDisabled ? "inconsistent" :
        c.SiubenActivated ? "enabled" : c.SiubenDisabled ? "disabled" : "pending";
}

public record ClientListResponse(IReadOnlyList<Entities.Client> Items, int Total, int Page, int PageSize);
