using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Exatech_Indotel_API.Repositories.ClientRepository;
using Exatech_Indotel_API.Repositories.UserRepository;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Exatech_Indotel_API.Services.Authorization;
using Exatech_Indotel_API.Services.Clients;
using Exatech_Indotel_API.Services.Siuben;
using Exatech_Indotel_API.Services.Wispro;

namespace Exatech_Indotel_API
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            services.AddTransient<IClientRepository, ClientRepository>();
            services.AddTransient<IUserRepository, UserRepository>();

            return services; 
        }

        public static IServiceCollection AddServices (this IServiceCollection services)
        {
            services.AddTransient<ISiubenApiProxy, SiubenApiProxy>();
            services.AddTransient<IWisproApiProxy, WisproApiProxy>();
            services.AddTransient<IClientsService, ClientsService>();
            services.AddTransient<IAuthorizationService, AuthorizationService>();

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
