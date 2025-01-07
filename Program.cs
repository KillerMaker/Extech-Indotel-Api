using Exatech_Indotel_API.Services.Email;
using Exatech_Indotel_API.Services.Siuben;
using Exatech_Indotel_API.Services.Wispro;
using Exatech_Indotel_API.Utilities;

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

            app.MapGet("/", (HttpContext httpContext) =>
            {

            });
            

            app.Run();
        }
    }
}
