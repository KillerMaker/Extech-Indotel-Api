using System.Collections.Concurrent;
using System.Text.Json;
using Exatech_Indotel_API.Entities;
using Exatech_Indotel_API.Models.Email;
using Exatech_Indotel_API.Repositories.ClientRepository;
using Exatech_Indotel_API.Repositories.UserRepository;
using Exatech_Indotel_API.Services.Cache;
using Exatech_Indotel_API.Services.Email;
using Exatech_Indotel_API.Services.Quartz;
using Exatech_Indotel_API.Utilities.Extensions;

namespace Exatech_Indotel_API.Simulation;

public static class SimulationSetup
{
    public static void Configure(WebApplicationBuilder builder)
    {
        // Last configuration provider wins, including over user-secrets/environment variables.
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["SecretKey"] = "simulation-only-key-never-use-in-production-2026",
            ["DatabaseConnectionString"] = "SIMULATION-NO-DATABASE",
            ["WisproClientsUrl"] = "http://127.0.0.1:5199/api/v1/clients",
            ["WisproContractsUrl"] = "http://127.0.0.1:5199/api/v1/contracts",
            ["WisproApiKey"] = "simulation-wispro",
            ["Siuben:Phase1:Url"] = "http://127.0.0.1:5199",
            ["Siuben:Phase2:Url"] = "http://127.0.0.1:5199",
            ["Siuben:Phase1:Username"] = "phase1",
            ["Siuben:Phase2:Username"] = "phase2",
            ["Siuben:Phase1:Password"] = "simulation",
            ["Siuben:Phase2:Password"] = "simulation",
            ["Siuben:Phase2:Provider"] = "Exatech"
        });
        builder.WebHost.UseUrls("http://127.0.0.1:5188");
    }

    public static void AddServices(IServiceCollection services)
    {
        services.AddSingleton<IClientRepository, SimulationClients>();
        services.AddSingleton<Exatech_Indotel_API.Repositories.PortalContentRepository.IPortalContentRepository, SimulationPortalContent>();
        services.AddSingleton<IUserRepository, SimulationUsers>();
        services.AddSingleton<ICacheService, SimulationCache>();
        services.AddSingleton<IEmailSenderService, SimulationEmail>();
        services.AddTransient<ScheduledJob>();
        services.ConfigureHttpClientDefaults(http => http
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
            .AddHttpMessageHandler(() => new SimulationHttpGuard()));
    }
}

sealed class SimulationHttpGuard : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is not { Scheme: "http", Host: "127.0.0.1", Port: 5199 })
            throw new InvalidOperationException("Simulation blocks all HTTP destinations except the local mock server.");
        return base.SendAsync(request, cancellationToken);
    }
}

public sealed class SimulationClients : IClientRepository
{
    private readonly ConcurrentDictionary<string, Client> rows = new();
    private int nextId = SimulationFixtures.Clients.Max(seed => seed.Id);
    private static string Key(string document) => document.Replace("-", "").Trim();
    public SimulationClients()
    {
        foreach (var seed in SimulationFixtures.Clients)
        {
            var id = seed.Id;
            rows[seed.Document] = new Client {
                ClientId = id, PublicId = 1000 + id, WisproId = "sim-" + seed.Document, CreatedById = 1,
                Name = "Cliente simulado " + id, PobertyLevel = 2, NationalIdentificationNumber = seed.Document,
                Locality = "Bombita", ContractNumber = seed.HasContract ? (980 + id).ToString() : null, Phone = "8095551234",
                Email = "cliente" + id + "@example.com", Street = "Calle simulada", Number = "12", City = "Barahona", State = "Barahona",
                SiubenActivated = seed.Activated, SiubenDisabled = seed.Disabled
            };
        }
    }
    public Task<Client?> GetByDocumentNumber(string documentNumber) => Task.FromResult(rows.GetValueOrDefault(Key(documentNumber)));
    public Task<Client?> GetByWisproId(string id) => Task.FromResult(rows.Values.FirstOrDefault(c => c.WisproId == id));
    public Task<Client?> GetByEmail(string email) => Task.FromResult(rows.Values.FirstOrDefault(c => c.Email == email));
    public Task<IEnumerable<Client>> GetClientsForContractSynchronization() => Task.FromResult<IEnumerable<Client>>(rows.Values.ToArray());
    public Task<bool> Exists(string document) => Task.FromResult(rows.ContainsKey(Key(document)));
    public Task Create(Client client) {
        client.ClientId = Interlocked.Increment(ref nextId);
        if (!rows.TryAdd(Key(client.NationalIdentificationNumber), client)) throw new InvalidOperationException("Client already exists.");
        return Task.CompletedTask;
    }
    public async Task AddContractNumber(string id, string contract) { var c = await GetByWisproId(id); if (c is not null) c.ContractNumber = contract; }
    public async Task MarkSiubenActivated(string id, string contract) { var c = await GetByWisproId(id); if (c is not null) { c.ContractNumber = contract; c.SiubenActivated = true; c.SiubenDisabled = false; } }
    public async Task MarkSiubenDisabled(string id, string contract) { var c = await GetByWisproId(id); if (c is not null) { c.ContractNumber = contract; c.SiubenActivated = false; c.SiubenDisabled = true; } }
}

sealed class SimulationUsers : IUserRepository
{
    private readonly ConcurrentDictionary<int, User> users = new();
    private int nextId = 2;
    public SimulationUsers() { users[1] = new User { UserId = 1, Email = "sim@example.com", Password = "Simulation123!".Sha256Hash(), Name = "Simulacion", Phone = "8095551234", RoleId = 1, Role = new Role { RoleId = 1, RoleName = "Admin" } };
        users[2] = new User { UserId = 2, Email = "seller@example.com", Password = "Simulation123!".Sha256Hash(), Name = "Vendedor simulado", Phone = "8095551234", RoleId = 2, Role = new Role { RoleId = 2, RoleName = "Seller" } };
    }
    public Task<User?> GetUserByEmail(string email) => Task.FromResult(users.Values.FirstOrDefault(u => u.Email == email));
    public Task<User?> CheckUserEmailAndPassword(string email, string password) => Task.FromResult(users.Values.FirstOrDefault(u => u.Email == email && u.Password == password));
    public Task<User?> GetUserById(int id) => Task.FromResult(users.GetValueOrDefault(id));
    public Task CreateUser(User user) { user.UserId = Interlocked.Increment(ref nextId); user.Role = new Role { RoleId = user.RoleId, RoleName = user.RoleId == 1 ? "Admin" : "Seller" }; users[user.UserId] = user; return Task.CompletedTask; }
    public Task DeleteUser(int id) { users.TryRemove(id, out _); return Task.CompletedTask; }
}

sealed class SimulationCache : ICacheService
{
    private readonly ConcurrentDictionary<string, (string Json, DateTimeOffset Expires)> entries = new();
    private readonly SemaphoreSlim gate = new(1);
    public Task<T?> GetAsync<T>(string key) where T : class => Task.FromResult(entries.TryGetValue(key, out var entry) && entry.Expires > DateTimeOffset.UtcNow ? JsonSerializer.Deserialize<T>(entry.Json) : null);
    public Task SetAsync<T>(string key, T value, TimeSpan lifetime) where T : class { entries[key] = (JsonSerializer.Serialize(value), DateTimeOffset.UtcNow.Add(lifetime)); return Task.CompletedTask; }
    public async Task<T> GetOrCreateAsync<T>(string key, TimeSpan lifetime, Func<Task<T>> create, Func<T, bool>? isValid = null) where T : class {
        await gate.WaitAsync();
        try { var existing = await GetAsync<T>(key); if (existing is not null && (isValid?.Invoke(existing) ?? true)) return existing; var value = await create(); await SetAsync(key, value, lifetime); return value; }
        finally { gate.Release(); }
    }
}

public sealed class SimulationEmail(ILogger<SimulationEmail> logger) : IEmailSenderService
{
    public ConcurrentQueue<string> Events { get; } = new();
    private Task Record(EventType type) { Events.Enqueue(type.ToString()); logger.LogInformation("SIMULATION: suppressed email {EventType}", type); return Task.CompletedTask; }
    public Task SendEmail(Client data, EventType eventType, IEnumerable<string>? copies = null) => Record(eventType);
    public Task SendEmail<T>(T data, EventType eventType, Func<EmailTemplate,T,string> fillTemplate, IEnumerable<string>? copies = null) => Record(eventType);
}
