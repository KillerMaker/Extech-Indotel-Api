using Microsoft.AspNetCore.HttpOverrides;
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
                    policy =>
                    {
                        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                        if (builder.Environment.IsDevelopment() && origins.Length == 0)
                            policy.AllowAnyOrigin();
                        else
                            policy.WithOrigins(origins);

                        policy.AllowAnyMethod().AllowAnyHeader();
                    });
            });

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                var networks = builder.Configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? [];
                foreach (var network in networks)
                    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
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
            if (builder.Configuration.GetValue("ScheduledJob:Enabled", true))
                builder.Services.AddQuartzScheduledJob();

            var app = builder.Build();

            app.UseForwardedHeaders();
            app.UseExceptionHandler("/Error");
            app.UseCors();
            if (builder.Configuration.GetValue("Swagger:Enabled", builder.Environment.IsDevelopment()))
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

            app.Run();
        }
    }
}
