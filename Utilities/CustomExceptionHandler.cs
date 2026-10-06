using Microsoft.AspNetCore.Diagnostics;
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
            var message = exception.Message;

            _logger.LogError(exception, message);

            httpContext.Response.ContentType = "application/json";
            httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = new
            {
                message = exception.Message
            };

            var result = JsonSerializer.Serialize(response);

            await httpContext.Response.WriteAsync(result);

            return true;
        }
    }
}
