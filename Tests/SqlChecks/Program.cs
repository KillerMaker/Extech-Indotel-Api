using Exatech_Indotel_API.Entities;
using Exatech_Indotel_API.Repositories.ClientRepository;
using Exatech_Indotel_API.Repositories.EmailTemplateRepository;
using Exatech_Indotel_API.Repositories.UserRepository;
using Exatech_Indotel_API.Services.Cache;
using Exatech_Indotel_API.Utilities.Extensions;
using Exatech_Indotel_API.Utilities.Factories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

var connectionString = Environment.GetEnvironmentVariable("SQL_CHECK_CONNECTION_STRING")
    ?? throw new InvalidOperationException("SQL_CHECK_CONNECTION_STRING must point to an isolated test database.");
var services = new ServiceCollection();
services.AddSingleton<IDatabaseConnectionFactory>(new TestConnectionFactory(connectionString));
services.AddSingleton<ICacheService>(new TestCache());
services.AddRepositories();
using var provider = services.BuildServiceProvider();
var users = provider.GetRequiredService<IUserRepository>();
var clients = provider.GetRequiredService<IClientRepository>();
var templates = provider.GetRequiredService<IEmailTemplateRepository>();
var suffix = Guid.NewGuid().ToString("N");
var passwordHash = "IsolatedSqlTest123!".Sha256Hash();
var email = $"sql-{suffix}@example.invalid";
var assertions = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
    assertions++;
}

await users.CreateUser(new User { Email = email, Password = passwordHash, Name = "SQL test", Phone = "8095551234", RoleId = 2 });
var created = await users.GetUserByEmail(email);
Check(created is { UserId: > 0, RoleId: 2, Role.RoleName: "Seller" }, "User lookup must map the user and role IDs.");
Check((await users.GetUserById(created!.UserId))?.Email == email, "User lookup by ID.");
Check((await users.CheckUserEmailAndPassword(email, passwordHash))?.Role?.RoleName == "Seller", "Login must map its role.");
Check(await users.CheckUserEmailAndPassword(email, "incorrect") is null, "Wrong password must not authenticate.");
Check(await users.GetUserById(-1) is null, "Missing user must return null.");

var client = new Client {
    PublicId = 1234, WisproId = suffix, CreatedById = created.UserId, Name = "Cliente prueba",
    PobertyLevel = 2, Email = email, Street = "Calle prueba", Number = "1",
    Phone = "8095551234", PhoneMobile = "8095555678", NationalIdentificationNumber = "001-1234567-0",
    City = "Barahona", State = "Barahona", Locality = "El Naranjo"
};
await clients.Create(client);
Check(await clients.Exists(client.NationalIdentificationNumber), "Client existence lookup.");
var found = await clients.GetByDocumentNumber("00112345670");
Check(found is { ClientId: > 0, Locality: "El Naranjo", SiubenActivated: false, SiubenDisabled: false }, "Client fields and default status.");
Check((await clients.GetByWisproId(suffix))?.CreatedById == created.UserId, "Wispro lookup and creator mapping.");
Check((await clients.GetByEmail(email))?.WisproId == suffix, "Client email lookup.");
await clients.AddContractNumber(suffix, "CONTRACT-TEST");
Check((await clients.GetByWisproId(suffix))?.ContractNumber == "CONTRACT-TEST", "Contract update.");
await clients.MarkSiubenActivated(suffix, "CONTRACT-TEST");
Check(await clients.GetByWisproId(suffix) is { SiubenActivated: true, SiubenDisabled: false }, "Activation flags.");
await clients.MarkSiubenDisabled(suffix, "CONTRACT-TEST");
Check(await clients.GetByWisproId(suffix) is { SiubenActivated: false, SiubenDisabled: true }, "Deactivation flags.");
Check((await clients.GetClientsForContractSynchronization()).Any(x => x.WisproId == suffix), "Scheduled synchronization query.");
foreach (var name in new[] { "ClientCreated", "ClientUpdated", "UserCreated" })
    Check(!string.IsNullOrWhiteSpace((await templates.Get(name)).Template), "Email template seed: " + name);
try
{
    client.WisproId = Guid.NewGuid().ToString("N");
    client.NationalIdentificationNumber = "00112345670";
    await clients.Create(client);
    throw new InvalidOperationException("Duplicate normalized document was accepted.");
}
catch (SqlException error) when (error.Number is 2601 or 2627) { assertions++; }
var content = provider.GetRequiredService<Exatech_Indotel_API.Repositories.PortalContentRepository.IPortalContentRepository>();
await content.Save(new Dictionary<string, string> { ["login.title"] = "Configuración persistente" }, created.UserId);
Check((await content.GetAll())["login.title"] == "Configuración persistente", "Portal messages persist with application SQL permissions.");
await content.Save(new Dictionary<string, string> { ["login.title"] = "Actualizado", ["home.title"] = "Inicio" }, created.UserId);
Check((await content.GetAll())["login.title"] == "Actualizado", "Existing messages update without duplicate keys.");
try
{
    await content.Save(new Dictionary<string, string> { ["login.title"] = "Invalid owner" }, -1);
    throw new InvalidOperationException("Missing administrator FK was accepted.");
}
catch (SqlException error) when (error.Number == 547) { assertions++; }
Check((await content.GetAll())["login.title"] == "Actualizado", "Failed portal settings transaction retains previous published content.");
Console.WriteLine($"Passed {assertions} SQL repository checks.");

sealed class TestConnectionFactory(string connectionString) : IDatabaseConnectionFactory
{
    public SqlConnection GetOpenConnection() => new(connectionString);
}

sealed class TestCache : ICacheService
{
    public Task<T?> GetAsync<T>(string key) where T : class => Task.FromResult<T?>(null);
    public Task SetAsync<T>(string key, T value, TimeSpan lifetime) where T : class => Task.CompletedTask;
    public Task<T> GetOrCreateAsync<T>(string key, TimeSpan lifetime, Func<Task<T>> create, Func<T, bool>? isValid = null) where T : class => create();
}
