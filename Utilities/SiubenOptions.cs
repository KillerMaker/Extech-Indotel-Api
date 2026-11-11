namespace Exatech_Indotel_API.Utilities;

public class SiubenOptions
{
    public SiubenPhaseOptions Phase1 { get; set; } = new();
    public SiubenPhaseOptions Phase2 { get; set; } = new();
}

public class SiubenPhaseOptions
{
    public string DisabledContractReason { get; set; } = "Cancelaci\u00f3n";
    public string? DisabledContractOtherReason { get; set; }
    public string Provider { get; set; } = "Exatech";
    public string[] Localities { get; set; } = [];
    public string Url { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
