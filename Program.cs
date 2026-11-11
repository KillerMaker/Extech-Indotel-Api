using Exatech_Indotel_API.Utilities;
using Exatech_Indotel_API.Utilities.Extensions;
using Exatech_Indotel_API.Utilities.Factories;

namespace Exatech_Indotel_API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(
                    policy => policy
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader());
            });

            builder.AddJwtAuthentication();
            builder.Services.AddAuthorization();

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddExceptionHandler<CustomExceptionHandler>();

            builder.Services.AddRedisCache();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddHttpClient();
            builder.Services.AddOptions<AppOptions>().Bind(builder.Configuration);

            builder.Services.AddSingleton<IDatabaseConnectionFactory, DatabaseConnectionFactory>();
            builder.Services.AddRepositories();
            builder.Services.AddServices();
            builder.Services.AddValidators();
            builder.Services.AddEmailProducer();
            builder.Services.AddQuartzScheduledJob();

            var app = builder.Build();

            app.UseExceptionHandler("/Error");
            app.UseCors();
            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseHttpsRedirection();
            app.MapControllers();
            app.UseAuthorization();

            app.Run();
        }
    }
}
