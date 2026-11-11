using Exatech_Indotel_API.Controllers;
using Exatech_Indotel_API.Services.Email;
using Exatech_Indotel_API.Services.Quartz;
using Exatech_Indotel_API.Utilities;
using Exatech_Indotel_API.Utilities.Extensions;

namespace Exatech_Indotel_API.Simulation;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            EnvironmentName = "Simulation",
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddJsonFile("simulation.settings.json", optional: false);
        SimulationSetup.Configure(builder);
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
        builder.AddJwtAuthentication();
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(ClientController).Assembly)
            .ConfigureApplicationPartManager(parts =>
            {
                // The API's legacy Swagger dependency is not needed by the simulation host.
                foreach (var part in parts.ApplicationParts.Where(part => part.Name.StartsWith("Swashbuckle", StringComparison.Ordinal)).ToArray())
                    parts.ApplicationParts.Remove(part);
            });
        builder.Services.AddExceptionHandler<CustomExceptionHandler>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHttpClient();
        builder.Services.AddOptions<AppOptions>().Bind(builder.Configuration);
        builder.Services.AddServices();
        builder.Services.AddValidators();
        SimulationSetup.AddServices(builder.Services);

        await using var api = builder.Build();
        api.UseExceptionHandler("/Error");
        api.UseCors();
        api.UseAuthentication();
        api.UseAuthorization();
        api.MapControllers();
        api.MapGet("/simulation/health", () => new { environment = "Simulation", database = "In-memory", outbound = "Local mocks only" });
        api.MapPost("/simulation/synchronize", async (ScheduledJob job) =>
        {
            await job.Execute(null!);
            return Results.Ok(new { message = "Synchronization completed" });
        }).RequireAuthorization(policy => policy.RequireRole("Admin"));
        api.MapGet("/simulation/emails", (IEmailSenderService email) => ((SimulationEmail)email).Events.ToArray()).RequireAuthorization();

        await using var mocks = MockServices.Create();
        await mocks.StartAsync();
        try { await api.RunAsync(); }
        finally { await mocks.StopAsync(); }
    }
}
