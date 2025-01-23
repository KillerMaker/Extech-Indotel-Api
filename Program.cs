using Exatech_Indotel_API.Services.Clients;
using Exatech_Indotel_API.Services.Email;
using Exatech_Indotel_API.Services.Siuben;
using Exatech_Indotel_API.Services.Wispro;
using Exatech_Indotel_API.Utilities;
using Exatech_Indotel_API.Services.Authorization;
using Exatech_Indotel_API.Utilities.Factories;

namespace Exatech_Indotel_API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.AddJwtAuthentication();

            builder.AddEventHubConsumer();

            builder.AddEventHubProducer();

            builder.Services.AddQuartzScheduledJob();

            builder.Services.AddMemoryCache();

            builder.Services.AddControllers();

            builder.Services.AddHttpContextAccessor();

            builder.Services.AddAuthorization();

            builder.Services.AddEndpointsApiExplorer();

            builder.Services.AddSwaggerGen();

            builder.Services.AddHttpClient();

            builder.Services.AddOptions<AppOptions>().Bind(builder.Configuration);

            builder.Services.AddSingleton<IDatabaseConnectionFactory, DatabaseConnectionFactory>();

            builder.Services.AddRepositories();

            builder.Services.AddServices();

            builder.Services.AddHostedService<EmailProcessorBackgroundService>();

            var app = builder.Build();

            app.UseSwagger();

            app.UseSwaggerUI();

            app.UseHttpsRedirection();

            app.MapControllers();

            app.UseAuthorization();

            app.Run();
        }
    }
}
