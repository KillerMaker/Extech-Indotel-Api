using System.Net;

namespace Exatech_Indotel_API.Services.Siuben;

public class SiubenApiException(HttpStatusCode statusCode, string message,
    string? customTitle = null, string? customMessage = null, string? extraInformation = null) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string? CustomTitle { get; } = customTitle;
    public string? CustomMessage { get; } = customMessage;
    public string? ExtraInformation { get; } = extraInformation;
}
