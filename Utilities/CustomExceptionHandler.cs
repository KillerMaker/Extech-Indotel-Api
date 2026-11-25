using Microsoft.AspNetCore.Diagnostics;
using Exatech_Indotel_API.Services.Siuben;
using System.Net;
using System.Text.Json;

namespace Exatech_Indotel_API.Utilities
{
    public class CustomExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<CustomExceptionHandler> _logger;
        public CustomExceptionHandler(ILogger<CustomExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var traceId = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
            _logger.LogError("Request failed with {ExceptionType}. Stack: {StackTrace}. TraceId: {TraceId}",
                exception.GetType().FullName, exception.StackTrace, traceId);
            System.Diagnostics.Activity.Current?.SetStatus(System.Diagnostics.ActivityStatusCode.Error);

            httpContext.Response.ContentType = "application/json";
            var siuben = exception as SiubenApiException;
            var apiError = exception as ApiException;
            var businessSiuben = siuben?.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or
                HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity;
            var customForbidden = siuben?.StatusCode == HttpStatusCode.Forbidden &&
                (siuben.CustomTitle is not null || siuben.CustomMessage is not null);
            httpContext.Response.StatusCode = apiError is not null ? (int)apiError.StatusCode : siuben is not null
                ? (customForbidden ? 422 : businessSiuben ? (int)siuben.StatusCode : 502) :
                exception is TimeoutException or TaskCanceledException ? 504 : 500;

            var safeMessage = apiError?.Message ?? siuben?.Message ??
                (httpContext.Response.StatusCode == 504 ? "La solicitud tardó demasiado. Verifique si la operación se completó antes de intentarlo de nuevo." :
                "No se pudo completar la operación. Contacte al administrador e indique el identificador de seguimiento.");

            var response = new
            {
                title = apiError?.Title ?? siuben?.CustomTitle ?? "No se pudo completar la operación",
                message = safeMessage,
                customTitle = siuben?.CustomTitle,
                customMessage = siuben?.CustomMessage,
                extraInformation = siuben?.ExtraInformation,
                code = apiError?.Code ?? (siuben is not null ? businessSiuben || customForbidden ? "SIUBEN_BUSINESS_ERROR" : "SIUBEN_UNAVAILABLE" :
                    httpContext.Response.StatusCode == 504 ? "REQUEST_TIMEOUT" : "INTERNAL_ERROR"),
                source = apiError?.SourceService ?? (siuben is not null ? "siuben" : "api"),
                status = httpContext.Response.StatusCode,
                errors = apiError?.Errors,
                traceId
            };

            var result = JsonSerializer.Serialize(response);

            await httpContext.Response.WriteAsync(result, cancellationToken);

            return true;
        }
    }
}
