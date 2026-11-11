using StackExchange.Redis;
using Exatech_Indotel_API.Services.Cache;
using Confluent.Kafka;

using Microsoft.IdentityModel.Tokens;
using System.Text;
using Exatech_Indotel_API.Repositories.ClientRepository;
using Exatech_Indotel_API.Repositories.UserRepository;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Exatech_Indotel_API.Services.Authorization;
using Exatech_Indotel_API.Services.Clients;
using Exatech_Indotel_API.Services.Siuben;
using Exatech_Indotel_API.Services.Siuben.Phase1;
using Exatech_Indotel_API.Services.Siuben.Phase2;
using Exatech_Indotel_API.Services.Wispro;
using Exatech_Indotel_API.Services.Email;
using Exatech_Indotel_API.Repositories.EmailTemplateRepository;
using Exatech_Indotel_API.Services.User;
using Quartz;
using Exatech_Indotel_API.Services.Quartz;
using FluentValidation;
using Exatech_Indotel_API.Utilities.Validators;

namespace Exatech_Indotel_API.Utilities.Extensions
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddRedisCache(this IServiceCollection services)
        {
            services.AddOptions<RedisOptions>().BindConfiguration("Redis")
                .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "Redis:ConnectionString is required.")
                .Validate(options => !string.IsNullOrWhiteSpace(options.InstanceName) && !options.InstanceName.Contains('{') && !options.InstanceName.Contains('}'),
                    "Redis:InstanceName is required and cannot contain braces.")
                .ValidateOnStart();
            services.AddSingleton<IConnectionMultiplexer>(provider =>
            {
                var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RedisOptions>>().Value;
                return ConnectionMultiplexer.Connect(options.ConnectionString);
            });
            services.AddSingleton<IDatabase>(provider => provider.GetRequiredService<IConnectionMultiplexer>().GetDatabase());
            services.AddSingleton<ICacheService, RedisCacheService>();

            return services;
        }

        public static IServiceCollection AddEmailProducer(this IServiceCollection services)
        {
            services.AddOptions<EmailQueueOptions>()
                .BindConfiguration(EmailQueueOptions.SectionName)
                .Validate(options => !string.IsNullOrWhiteSpace(options.BootstrapServers), "EmailQueue:BootstrapServers is required.")
                .Validate(options => !string.IsNullOrWhiteSpace(options.Topic), "EmailQueue:Topic is required.")
                .Validate(options => !string.IsNullOrWhiteSpace(options.System), "EmailQueue:System is required.")
                .ValidateOnStart();

            services.AddSingleton<IProducer<Null, string>>(provider =>
            {
                var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailQueueOptions>>().Value;
                return new ProducerBuilder<Null, string>(new ProducerConfig
                {
                    BootstrapServers = options.BootstrapServers,
                    EnableIdempotence = true,
                    MessageTimeoutMs = 30000
                }).Build();
            });

            return services;
        }

        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            services.AddTransient<IClientRepository, ClientRepository>();
            services.AddTransient<IUserRepository, UserRepository>();
            services.AddTransient<IEmailTemplateRepository, EmailTemplateRepository>();
            services.AddTransient<IUserRepository, UserRepository>();

            return services;
        }

        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            services.AddSingleton<SiubenLocalities>();
            services.AddOptions<SiubenOptions>().BindConfiguration("Siuben")
                .Validate(SiubenLocalities.IsValidConfiguration,
                    "Each Siuben phase must configure non-empty Localities, with no blank or duplicate names across either phase.")
                .ValidateOnStart()
                .PostConfigure<Microsoft.Extensions.Options.IOptions<AppOptions>>((options, legacy) =>
                {
                    if (string.IsNullOrWhiteSpace(options.Phase1.Url)) options.Phase1.Url = legacy.Value.SiubenUrl;
                    if (string.IsNullOrWhiteSpace(options.Phase1.Username)) options.Phase1.Username = legacy.Value.SiubenUsername;
                    if (string.IsNullOrWhiteSpace(options.Phase1.Password)) options.Phase1.Password = legacy.Value.SiubenPassword;
                });
            services.AddTransient<SiubenPhase1ApiProxy>();
            services.AddTransient<SiubenPhase2ApiProxy>();
            services.AddTransient<ISiubenApiProxyResolver, SiubenApiProxyResolver>();
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

        public static IServiceCollection AddQuartzScheduledJob(this IServiceCollection services)
        {
            services.AddQuartz(configure =>
            {
                var jobKey = new JobKey(nameof(ScheduledJob));

                configure.AddJob<ScheduledJob>(jobKey)
                    .AddTrigger(trigger => trigger
                        .ForJob(jobKey)
                        .WithDailyTimeIntervalSchedule(24, IntervalUnit.Hour, builder => builder
                            .StartingDailyAt(new TimeOfDay(23, 59))));
            });

            services.AddQuartzHostedService(options =>
            {
                options.WaitForJobsToComplete = true;
            });

            return services;
        }

        public static IServiceCollection AddValidators(this IServiceCollection services)
        {
            services.AddValidatorsFromAssemblyContaining<CreateClientValidator>();
            services.AddValidatorsFromAssemblyContaining<CreateUserValidator>();

            return services;
        }

    }
}
