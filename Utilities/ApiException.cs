using System.Net;

namespace Exatech_Indotel_API.Utilities;

public sealed class ApiException(HttpStatusCode statusCode, string code, string title, string message,
    string? sourceService = null, IReadOnlyDictionary<string, string[]>? errors = null) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public string Title { get; } = title;
    public string? SourceService { get; } = sourceService;
    public IReadOnlyDictionary<string, string[]>? Errors { get; } = errors;
}
