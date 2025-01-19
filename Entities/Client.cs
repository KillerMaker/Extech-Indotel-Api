namespace Exatech_Indotel_API.Entities
{
    public class Client
    {
        public int ClientId { get; set; }

        public required string WisproId { get; set;}

        public required int CreatedById { get; set; }

        public required string Name { get; set; }

        public required int PobertyLevel {  get; set; }

        public string? ContractNumber { get; set; } = null;

        public string? Email { get; set; } = string.Empty;

        public string Street { get; set; } = string.Empty;

        public string Number { get; set; } = string.Empty;

        public string? Phone { get; set; } = string.Empty;

        public string? PhoneMobile { get; set; } = string.Empty;

        public required string NationalIdentificationNumber { get; set; }

        public string? City { get; set; } = string.Empty;

        public string? State { get; set; } = string.Empty;


    }
}
