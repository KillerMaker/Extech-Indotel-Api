
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Exatech_Indotel_API.Repositories.ClientRepository;
using Exatech_Indotel_API.Repositories.UserRepository;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Exatech_Indotel_API.Services.Authorization;
using Exatech_Indotel_API.Services.Clients;
using Exatech_Indotel_API.Services.Siuben;
using Exatech_Indotel_API.Services.Wispro;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Azure.Storage.Blobs;
using Azure.Messaging.EventHubs.Consumer;
using Exatech_Indotel_API.Services.Email;
using Exatech_Indotel_API.Repositories.EmailTemplateRepository;
using Exatech_Indotel_API.Services.User;

namespace Exatech_Indotel_API
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddEventHubConsumer(this WebApplicationBuilder builder)
        {
            string blobStorageContainerName = builder.Configuration.GetValue<string>("BlobContainerName") ?? string.Empty;
            string blobStorageContainerConnectionString = builder.Configuration.GetValue<string>("BlobStorageConnectionString") ?? string.Empty;
            string eventHubName = builder.Configuration.GetValue<string>("EventHubName") ?? string.Empty;
            string eventHubConnectionString = builder.Configuration.GetValue<string>("EventHubConnectionString") ?? string.Empty;

            var options = new EventProcessorClientOptions()
            {
                RetryOptions = new EventHubsRetryOptions()
                {
                    MaximumRetries = int.Parse(Environment.GetEnvironmentVariable("MaxRetries") ?? "0"),
                    Delay = TimeSpan.FromSeconds(int.Parse(Environment.GetEnvironmentVariable("DelayInSeconds") ?? "120")),
                    MaximumDelay = TimeSpan.FromSeconds(int.Parse(Environment.GetEnvironmentVariable("DelayInSeconds") ?? "120"))
                }
            };

            var blobStorage = new BlobContainerClient(blobStorageContainerConnectionString, blobStorageContainerName);

            builder.Services.AddTransient(x => new EventProcessorClient(blobStorage, EventHubConsumerClient.DefaultConsumerGroupName, eventHubConnectionString, eventHubName, options));

            return builder.Services;
        }

        public static IServiceCollection AddEventHubProducer(this WebApplicationBuilder builder)
        {
            string eventHubName = builder.Configuration.GetValue<string>("EventHubName") ?? string.Empty;
            string eventHubConnectionString = builder.Configuration.GetValue<string>("EventHubConnectionString") ?? string.Empty;

            builder.Services.AddTransient(x => new EventHubProducerClient(eventHubConnectionString, eventHubName));

            return builder.Services;
        }

        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            services.AddTransient<IClientRepository, ClientRepository>();
            services.AddTransient<IUserRepository, UserRepository>();
            services.AddTransient<IEmailTemplateRepository, EmailTemplateRepository>();
            services.AddTransient<IUserRepository, UserRepository>();

            return services; 
        }

        public static IServiceCollection AddServices (this IServiceCollection services)
        {
            services.AddTransient<ISiubenApiProxy, SiubenApiProxy>();
            services.AddTransient<IWisproApiProxy, WisproApiProxy>();
            services.AddTransient<IClientsService, ClientsService>();
            services.AddTransient<IAuthorizationService, AuthorizationService>();
            services.AddTransient<IEmailSenderService, EmailSenderService>();
            services.AddTransient<IUserService, UserService>();

            return services;
        }

        public static IServiceCollection AddJwtAuthentication(this WebApplicationBuilder builder)
        {
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(builder.Configuration.GetValue<string>("SecretKey") ??
                            throw new NullReferenceException()))
                    };
                });

            return builder.Services;
        }

    }
}
