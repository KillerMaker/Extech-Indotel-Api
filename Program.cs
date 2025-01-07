using Exatech_Indotel_API.Services.Clients;
using Exatech_Indotel_API.Services.Email;
using Exatech_Indotel_API.Services.Siuben;
using Exatech_Indotel_API.Services.Wispro;
using Exatech_Indotel_API.Utilities;
using Microsoft.AspNetCore.Mvc;
using Exatech_Indotel_API.Models.Wispro;

namespace Exatech_Indotel_API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddAuthorization();

            builder.Services.AddOptions<AppOptions>().Bind(builder.Configuration);

            builder.Services.AddTransient<ISiubenApiProxy, SiubenApiProxy>();
            builder.Services.AddTransient<IWisproApiProxy, WisproApiProxy>();

            builder.Services.AddHostedService<EmailNotificationSender>();

            var app = builder.Build();

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapGet("clients/check", async (
                [FromServices] ClientsService clientsService,
                [FromQuery] string documentNumber,
                [FromQuery] string? phoneNumber,
                [FromQuery] string? email) => Results.Ok(await clientsService.CheckClient(documentNumber, phoneNumber, email))
            );

            app.MapPost("clients/create", async (
                [FromServices] ClientsService clientsService,
                [FromBody] WisproClient wisproClient) => Results.Ok(await clientsService.CreateClient(wisproClient))
            );


            app.Run();
        }
    }
}
