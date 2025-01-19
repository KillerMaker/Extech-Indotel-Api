using Azure;
using Exatech_Indotel_API.Entities;
using Exatech_Indotel_API.Models.Client;
using Exatech_Indotel_API.Models.Wispro;

namespace Exatech_Indotel_API.Utilities
{
    public static class ExtensionMethods
    {
        public static string FillTemplate(this EmailTemplate template, Client client)
        {
            return template.Template
                .Replace("$$ClientId$$", client.ClientId.ToString())
                .Replace("$$WisproId$$", client.WisproId)
                .Replace("$$CreatedById$$", client.CreatedById.ToString())
                .Replace("$$Name$$", client.Name)
                .Replace("$$PobertyLevel$$", client.PobertyLevel.ToString())
                .Replace("$$ContractNumber$$", client.ContractNumber ?? string.Empty)
                .Replace("$$Email$$", client.Email ?? string.Empty)
                .Replace("$$Street$$", client.Street)
                .Replace("$$Number$$", client.Number)
                .Replace("$$Phone$$", client.Phone ?? string.Empty)
                .Replace("$$PhoneMobile$$", client.PhoneMobile ?? string.Empty)
                .Replace("$$NationalIdentificationNumber$$", client.NationalIdentificationNumber)
                .Replace("$$City$$", client.City ?? string.Empty)
                .Replace("$$State$$", client.State ?? string.Empty);
        }
        public static Client ToClient(this ClientCreateDto client, string wisproId, string userId) =>
            new Client
            {
                WisproId = wisproId,
                CreatedById = int.Parse(userId),
                Name = client.Name,
                PobertyLevel = client.PobertyLevel,
                ContractNumber = null,
                Email = client.Email,
                Street = client.Street,
                Number = client.Number,
                Phone = client.Phone,
                PhoneMobile = client.PhoneMobile,
                NationalIdentificationNumber = client.NationalIdentificationNumber,
                City = client.City,
                State = client.State
            };

        public static WisproClient ToWisproClient(this ClientCreateDto client) =>
            new WisproClient
            {
                Name = client.Name,
                Email = client.Email,
                Street = client.Street,
                Number = client.Number,
                City = client.City,
                Phone =  client.Phone,
                PhoneMobile = client.PhoneMobile,
                State = client.State,
                NationalIdentificationNumber = client.NationalIdentificationNumber
            };

    }
}
