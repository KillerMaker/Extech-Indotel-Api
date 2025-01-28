using Azure;
using Exatech_Indotel_API.Entities;
using Exatech_Indotel_API.Models.Client;
using Exatech_Indotel_API.Models.User;
using Exatech_Indotel_API.Models.Wispro;
using System.Security.Cryptography;
using System.Text;

namespace Exatech_Indotel_API.Utilities.Extensions
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
                .Replace("$$State$$", client.State ?? string.Empty)
                .Replace("$$PublicId$$", client.PublicId.ToString());
        }
        public static Client ToClient(this CreateClientRequest client, string wisproId, string userId, int publicId) =>
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
                State = client.State,
                PublicId = publicId
            };

        public static WisproClient ToWisproClient(this CreateClientRequest client) =>
            new WisproClient
            {
                Name = client.Name,
                Email = client.Email,
                Street = client.Street,
                Number = client.Number,
                City = client.City,
                Phone = client.Phone,
                PhoneMobile = client.PhoneMobile,
                State = client.State,
                NationalIdentificationNumber = client.NationalIdentificationNumber
            };

        public static CreateClientResponse ToCreateClientResponse(this Client client) =>
            new CreateClientResponse
            {
                Name = client.Name,
                WisproId = client.WisproId,
                PublicId = client.PublicId,
                Email = client.Email ?? string.Empty,
                NationalIdentificationNumber = client.NationalIdentificationNumber,
                Street = client.Street,
                Number = client.Number,
                Phone = client.Phone ?? string.Empty,
                PhoneMobile = client.PhoneMobile ?? string.Empty,
                City = client.City ?? string.Empty,
                State = client.State ?? string.Empty,
                PobertyLevel = client.PobertyLevel
            };

        public static User ToUser(CreateUserRequest user) =>
            new User
            {
                Name = user.Name,
                Email = user.Email,
                Password = user.Password,
                RoleId = user.RoleId,
                Phone = user.Phone
            };

        public static string Sha256Hash(this string s)
        {
            var hashedPassBytes = SHA256.Create()
                    .ComputeHash(Encoding.UTF8.GetBytes(s));

            StringBuilder builder = new StringBuilder();

            foreach (var item in hashedPassBytes)
                builder.Append(item.ToString("x2"));

            return builder.ToString();
        }

    }
}
