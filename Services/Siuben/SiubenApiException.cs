using System.Net;

namespace Exatech_Indotel_API.Services.Siuben;

public class SiubenApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
