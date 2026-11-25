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
            httpContext.Response.StatusCode = exception is SiubenApiException siuben
                ? (int)(siuben.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound
                    ? siuben.StatusCode : HttpStatusCode.BadGateway)
                : (int)HttpStatusCode.InternalServerError;

            var response = new
            {
                message = exception.Message,
                traceId
            };

            var result = JsonSerializer.Serialize(response);

            await httpContext.Response.WriteAsync(result);

            return true;
        }
    }
}
