using System.Text.Json.Serialization;

namespace Exatech_Indotel_API.Models.Siuben
{
    public class GetBeneficiaryResponse
    {

        [JsonPropertyName("idHogar")]
        public required string HomeId { get; set; }

        [JsonPropertyName("cedula")]
        public required string DocumentNumber { get; set; }

        [JsonPropertyName("provincia")]
        public required string Province { get; set; }

        [JsonPropertyName("provinciaCodigo")]
        public required string ProvinceCode { get; set; }

        [JsonPropertyName("nombre")]
        public required string Name { get; set; }

        [JsonPropertyName("apellidos")]
        public required string LastName { get; set; }

        [JsonPropertyName("categoria_pobreza")]
        public required int? PobertyLevel { get; set; }

        [JsonPropertyName("categoriaIcv")]
        public required int? IcvCategory { get; set; }

        [JsonPropertyName("nocontrato")]
        public string? ContractNumber { get; set; }
    }
}
