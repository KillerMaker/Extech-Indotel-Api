using StackExchange.Redis;
using System.Reflection;
using Exatech_Indotel_API.Services.Quartz;
using System.Security.Claims;
using Exatech_Indotel_API.Entities;
using Exatech_Indotel_API.Repositories.ClientRepository;
using Exatech_Indotel_API.Services.Email;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.Json;
using Exatech_Indotel_API.Controllers;
using Exatech_Indotel_API.Services.Clients;
using Exatech_Indotel_API.Services.Wispro;
using Exatech_Indotel_API.Models.Wispro;
using Exatech_Indotel_API.Models.Client;
using Exatech_Indotel_API.Models.Siuben;
using Exatech_Indotel_API.Models.Siuben.Phase2;
using Exatech_Indotel_API.Services.Siuben;
using Exatech_Indotel_API.Services.Siuben.Phase1;
using Exatech_Indotel_API.Services.Siuben.Phase2;
using Exatech_Indotel_API.Utilities;
using Exatech_Indotel_API.Utilities.Extensions;
using Exatech_Indotel_API.Utilities.Validators;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Exatech_Indotel_API.Services.Cache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

var count = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception($"FAIL: {label}");
    count++;
}
async Task Throws<T>(Func<Task> action, string label) where T : Exception
{
    try { await action(); }
    catch (T) { count++; return; }
    throw new Exception($"FAIL: {label}");
}

using (var privateActivity = new System.Diagnostics.Activity("privacy-test"))
{
    privateActivity.SetTag("url.full", "https://siuben.test/api/data/00100118793?token=private");
    privateActivity.SetTag("http.url", "https://wispro.test/clients?email=private");
    privateActivity.SetTag("url.query", "document=private");
    privateActivity.SetTag("url.path", "/client/private");
    privateActivity.SetTag("exception.message", "private input");
    new PrivateUrlProcessor().OnEnd(privateActivity);
    Check((string?)privateActivity.GetTagItem("url.full") == "https://siuben.test/" &&
        (string?)privateActivity.GetTagItem("http.url") == "https://wispro.test/" &&
        privateActivity.GetTagItem("url.query") is null && privateActivity.GetTagItem("url.path") is null &&
        privateActivity.GetTagItem("exception.message") is null, "telemetry strips private URL paths, queries and exception text");
}

var configuration = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json").Build();
var localityOptions = configuration.GetSection("Siuben").Get<SiubenOptions>()!;
var localities = new SiubenLocalities(Options.Create(localityOptions));
var localityResponse = (Microsoft.AspNetCore.Mvc.OkObjectResult)new SiubenController(null!, localities, null!).GetLocalities();
var localityList = ((IEnumerable<SiubenLocality>)localityResponse.Value!).ToArray();
Check(localityList.Length == localityOptions.Phase1.Localities.Length + localityOptions.Phase2.Localities.Length,
    "localities endpoint exposes all configured names");
Check(localityList.Any(item => item.Name == "Bombita" && item.Phase == 2),
    "localities endpoint includes each locality's phase");

string[] phase1 = ["Salinas", "Cristobal", "Batey 8", "Santana", "Cabeza de Toro", "Quita Coraza", "Fondo Negro", "Canoa", "Jaquimeyes", "Palo Alto", "Fundacion", "Pescaderia", "La Guazara", "Polo"];
string[] phase2 = ["El Naranjo", "La lista", "Bombita", "El Memiso", "El higuito", "Las Minas", "Batey 7", "Batey 9", "Sabana Mula", "El Pon"];
foreach (var locality in phase1) Check(localities.GetPhase(locality) == SiubenPhase.Phase1, locality);
foreach (var locality in phase2) Check(localities.GetPhase(locality) == SiubenPhase.Phase2, locality);
Check(localities.GetName("  CRISTÓBAL  ") == "Cristobal", "accent and case normalization");
Check(localities.GetPhase("  batey   7 ") == SiubenPhase.Phase2, "space normalization");
foreach (var locality in new string?[] { null, "", "unknown", "7" }) Check(!localities.IsSupported(locality), "reject invalid locality");

var customOptions = new SiubenOptions
{
    Phase1 = new() { Localities = ["Custom North"] },
    Phase2 = new() { Localities = ["Custom South"] }
};
var customLocalities = new SiubenLocalities(Options.Create(customOptions));
Check(customLocalities.GetPhase("custom south") == SiubenPhase.Phase2, "routing uses configured names");
Check(!customLocalities.IsSupported("Salinas"), "no hardcoded locality fallback");
customOptions.Phase2.Localities = [" CUSTOM   NORTH "];
await Throws<OptionsValidationException>(() => Task.FromResult(new SiubenLocalities(Options.Create(customOptions))), "duplicate locality across phases rejected");
customOptions.Phase2.Localities = [" "];
Check(!SiubenLocalities.IsValidConfiguration(customOptions), "blank configured locality rejected");
customOptions.Phase2.Localities = [];
Check(!SiubenLocalities.IsValidConfiguration(customOptions), "missing locality list rejected");
customOptions.Phase2.Localities = ["Custom South", "custom south"];
Check(!SiubenLocalities.IsValidConfiguration(customOptions), "duplicate locality within a phase rejected");

var settings = Options.Create(new SiubenOptions
{
    Phase1 = new() { Url = "https://siuben.test", Username = "phase1-user", Password = "phase1-password" },
    Phase2 = new() { Url = "https://siuben.test", Username = "phase2-user", Password = "phase2-password" }
});
using var cache = new FakeCache();
var factory = new FakeFactory();
var first = new SiubenPhase1ApiProxy(settings, factory, cache);
var second = new SiubenPhase2ApiProxy(settings, factory, cache);
factory.Enqueue(HttpStatusCode.OK, """{"token":"token-one"}""");
factory.Enqueue(HttpStatusCode.OK, """
{"idHogar":"home","cedula":"00100118793","provincia":"Barahona","provinciaCodigo":"04","nombre":"Name","apellidos":"Last","categoria_pobreza":2,"categoriaIcv":1,"nocontrato":"contract"}
""");
var one = await first.GetContract("001-0011879-3");
Check(one.Exists && one.PovertyLevel == 2 && one.ContractNumber == "contract", "phase 1 response preserved");
Check(factory.Requests[1].Path == "/api/Data/get/00100118793", "phase 1 route and document normalization");
factory.Enqueue(HttpStatusCode.OK, """{"token":"token-two"}""");
factory.Enqueue(HttpStatusCode.OK, """{"razon":"Elegible"}""");
var two = await second.GetContract("00100118793");
Check(two.Exists && two.IsEligible == true && two.PovertyLevel is null && two.ContractNumber is null, "phase 2 eligibility without fabricated poverty or contract");
Check(factory.Requests[2].Body!.Contains("phase2-user"), "phase 2 login credentials");
Check(factory.Requests[1].Token == "token-one" && factory.Requests[3].Token == "token-two", "tokens isolated between phases");
Check(factory.Requests[3].Path == "/api/data_fase2/get/00100118793", "phase 2 route");
factory.Enqueue(HttpStatusCode.NotFound, """{"extraInformation":"Persona no encontrada"}""");
Check(!(await second.GetContract("00100118793")).Exists, "phase 2 missing beneficiary");
Check(factory.Requests.Count == 5, "token reused");
factory.Enqueue(HttpStatusCode.BadRequest, """{"extraInformation":"Persona no elegible - Forma parte de una prestadora"}""");
try { await second.GetContract("00100118793"); throw new Exception("Expected business error"); }
catch (SiubenApiException error)
{
    Check(error.StatusCode == HttpStatusCode.BadRequest && error.Message.Contains("prestadora"), "business error reason preserved");
}
factory.Enqueue(HttpStatusCode.Unauthorized, """{}""");
factory.Enqueue(HttpStatusCode.OK, """{"token":"refreshed"}""");
factory.Enqueue(HttpStatusCode.Unauthorized, """{}""");
await Throws<SiubenApiException>(() => second.GetContract("00100118793"), "401 retried only once");
Check(factory.Requests.Count == 9, "bounded unauthorized request count");
Check(factory.Requests[^1].Token == "refreshed", "retry uses new token");

factory.Enqueue(HttpStatusCode.OK, """{"razon":"Habilitado exitosamente para todo el hogar"}""");
var enabled = await second.EnableBeneficiary(new()
{
    DocumentNumber = "00100118793", PhoneNumber = "8095551234", Provider = "Exatech",
    ContractNumber = "CON-2026-1", ContractDate = DateTimeOffset.Parse("2026-09-14T00:00:00Z")
});
var enable = factory.Requests[^1];
using (var json = JsonDocument.Parse(enable.Body!))
{
    Check(enable.Method == HttpMethod.Put && enable.Path == "/api/data_fase2/HabilitarBeneficiado", "enable route");
    Check(json.RootElement.GetProperty("telefonoBeneficiario").GetString() == "8095551234" &&
        json.RootElement.GetProperty("numeroContrato").GetString() == "CON-2026-1" &&
        json.RootElement.GetProperty("fechaContrato").GetDateTimeOffset().Year == 2026, "enable payload");
}
await Throws<ValidationException>(() => second.EnableBeneficiary(new() { DocumentNumber = "bad" }), "invalid enable input rejected");
await Throws<ValidationException>(() => second.DisableBeneficiary(new() { DocumentNumber = "00100118793", CancellationReason = "Otro" }), "Otro requires description");
await Throws<ValidationException>(() => second.DisableBeneficiary(new() { DocumentNumber = "00100118793", CancellationReason = "Cancellation" }), "invalid cancellation reason rejected");
factory.Enqueue(HttpStatusCode.OK, """{"razon":"Deshabilitado"}""");
await second.DisableBeneficiary(new() { DocumentNumber = "00100118793", CancellationReason = "Otro", OtherCancellationReason = "Mudanza" });
Check(factory.Requests[^1].Path == "/api/data_fase2/DeshabilitarBeneficiado" && factory.Requests[^1].Body!.Contains("Mudanza"), "disable route and payload");
foreach (var reason in new[] { "Cancelación", "Falta de pago", "Error de inclusión" })
{
    foreach (var other in new string?[] { null, "", "Stale description" })
    {
        factory.Enqueue(HttpStatusCode.OK, """{"razon":"Deshabilitado"}""");
        await second.DisableBeneficiary(new() { DocumentNumber = "00100118793", CancellationReason = reason, OtherCancellationReason = other });
        using var cancellationJson = JsonDocument.Parse(factory.Requests[^1].Body!);
        Check(!cancellationJson.RootElement.TryGetProperty("otroMotivoCancelacion", out _), "non-Otro cancellation omits optional field entirely");
        Check(cancellationJson.RootElement.GetProperty("motivoCancelacion").GetString() == reason, "cancellation reason preserved");
    }
}
factory.Enqueue(HttpStatusCode.NotFound, """{}""");
Check(!(await first.GetContract("00100118793")).Exists, "phase 1 not found preserved");
factory.Enqueue(HttpStatusCode.OK, """{}""");
await first.PutContract("001-0011879-3", new() { ContractNumber = "1", DateCreated = "2026-11-03" });
Check(factory.Requests[^1].Path == "/api/Data/update/00100118793" && factory.Requests[^1].Body!.Contains("noContrato"), "phase 1 update preserved");

foreach (var baseUrl in new[] { "https://siuben.test/INDOTELAPI", "https://siuben.test/INDOTELAPI/" })
{
    using var prefixCache = new FakeCache();
    var prefixFactory = new FakeFactory();
    var prefixProxy = new SiubenPhase2ApiProxy(Options.Create(new SiubenOptions
    {
        Phase2 = new() { Url = baseUrl, Username = "test", Password = "test" }
    }), prefixFactory, prefixCache);
    prefixFactory.Enqueue(HttpStatusCode.OK, """{"token":"prefix-token"}""");
    prefixFactory.Enqueue(HttpStatusCode.OK, """{"razon":"Elegible"}""");
    await prefixProxy.GetContract("00100118793");
    Check(prefixFactory.Requests[0].Path == "/INDOTELAPI/api/auth/login", "virtual directory preserved for login");
    Check(prefixFactory.Requests[1].Path == "/INDOTELAPI/api/data_fase2/get/00100118793", "virtual directory preserved for lookup");
    prefixFactory.Enqueue(HttpStatusCode.OK, """{"razon":"Habilitado"}""");
    await prefixProxy.EnableBeneficiary(new()
    {
        DocumentNumber = "00100118793", PhoneNumber = "8095551234", Provider = "Exatech",
        ContractNumber = "1", ContractDate = DateTimeOffset.Parse("2026-09-14T00:00:00Z")
    });
    Check(prefixFactory.Requests[^1].Path == "/INDOTELAPI/api/data_fase2/HabilitarBeneficiado", "virtual directory preserved for enable");
    prefixFactory.Enqueue(HttpStatusCode.OK, """{"razon":"Deshabilitado"}""");
    await prefixProxy.DisableBeneficiary(new() { DocumentNumber = "00100118793", CancellationReason = "Cancelación" });
    Check(prefixFactory.Requests[^1].Path == "/INDOTELAPI/api/data_fase2/DeshabilitarBeneficiado", "virtual directory preserved for disable");
}

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddOptions<AppOptions>().Configure(o => { o.SiubenUrl = "https://legacy.test"; o.SiubenUsername = "legacy"; o.SiubenPassword = "secret"; });
services.AddSingleton<ICacheService>(cache);
services.AddSingleton<IHttpClientFactory>(factory);
services.AddServices();
using var provider = services.BuildServiceProvider();
var resolver = provider.GetRequiredService<ISiubenApiProxyResolver>();
Check(resolver.Resolve("Salinas") is SiubenPhase1ApiProxy, "legacy phase 1 configuration works without phase 2 credentials");
Check(provider.GetRequiredService<IOptions<SiubenOptions>>().Value.Phase2.Username == "", "phase 1 credentials not inherited by phase 2");
await Throws<InvalidOperationException>(() => Task.FromResult(resolver.Resolve("Bombita")), "missing phase 2 configuration fails explicitly");

var validator = new CreateClientValidator(localities);
var client = new CreateClientRequest { Name = "Name", Email = "test@example.com", NationalIdentificationNumber = "00100118793", Locality = "Bombita", PhoneMobile = "8095551234" };
Check(validator.Validate(client).IsValid, "phase 2 creation does not require unavailable poverty level");
client.Locality = "Salinas";
Check(!validator.Validate(client).IsValid, "phase 1 poverty requirement preserved");
client.PobertyLevel = 2; client.Locality = "unknown";
Check(!validator.Validate(client).IsValid, "invalid creation locality rejected");
client.Locality = "Salinas";
Check(client.ToClient("wispro", "1", 2).ToCreateClientResponse().Locality == "Salinas", "locality mapping preserved");
var handler = new CustomExceptionHandler(NullLogger<CustomExceptionHandler>.Instance);
var context = new DefaultHttpContext(); context.Response.Body = new MemoryStream();
await handler.TryHandleAsync(context, new SiubenApiException(HttpStatusCode.BadRequest, "Not eligible"), default);
Check(context.Response.StatusCode == 400, "Siuben business errors mapped to 400");
context = new DefaultHttpContext(); context.Response.Body = new MemoryStream();
await handler.TryHandleAsync(context, new SiubenApiException(HttpStatusCode.Unauthorized, "Authentication failed"), default);
Check(context.Response.StatusCode == 502, "upstream credential failures mapped to 502");
var customFactory = new FakeFactory();
var customProxy = new SiubenPhase2ApiProxy(settings, customFactory, new FakeCache());
customFactory.Enqueue(HttpStatusCode.OK, """{"token":"custom-token"}""");
const string customSiubenError = """{"customTitle":"Elemento no cumple los criterios","customMessage":"El elemento que esta consultando no cumple con criterios validos","extraInformation":"Persona no elegible - Forma parte de una prestadora","targetSite":null,"message":"Exception of type 'Indotel.Util.ErrorResponses.Exceptions.ItemNotValid_customExceptionException' was thrown.","data":{},"innerException":null,"stackTrace":"internal stack"}""";
foreach (var upstreamStatus in new[] { HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.Forbidden })
{
    customFactory.Enqueue(upstreamStatus, customSiubenError);
    try { await customProxy.GetContract("00100118793"); throw new Exception("Expected custom Siuben error"); }
    catch (SiubenApiException exception)
    {
        context = new DefaultHttpContext(); context.Response.Body = new MemoryStream();
        await handler.TryHandleAsync(context, exception, default);
        context.Response.Body.Position = 0;
        using var errorJson = await JsonDocument.ParseAsync(context.Response.Body);
        var body = errorJson.RootElement;
        Check(body.GetProperty("title").GetString() == "Elemento no cumple los criterios" &&
            body.GetProperty("customMessage").GetString()!.Contains("criterios validos") &&
            body.GetProperty("extraInformation").GetString()!.Contains("prestadora"), "Siuben custom title, explanation and reason reach the frontend");
        Check(!body.GetRawText().Contains("customException") && !body.TryGetProperty("stackTrace", out _) &&
            body.TryGetProperty("traceId", out _), "Siuben internal exception data stripped while correlation retained");
        Check(context.Response.StatusCode == (upstreamStatus == HttpStatusCode.Forbidden ? 422 : 400),
            "Siuben business envelopes use actionable client error statuses, including HTTP 200 envelopes");
    }
}
context = new DefaultHttpContext(); context.Response.Body = new MemoryStream();
await handler.TryHandleAsync(context, new InvalidOperationException("private database connection details"), default);
context.Response.Body.Position = 0;
Check(!(await new StreamReader(context.Response.Body).ReadToEndAsync()).Contains("private database"), "unexpected errors do not expose internal exception messages");
var clientsService = new ClientsService(new FixedResolver(first, second, localities), new FakeWispro(), null!, new HttpContextAccessor(), null!, localities);
factory.Enqueue(HttpStatusCode.OK, """
{"idHogar":"home","cedula":"00100118793","provincia":"Barahona","provinciaCodigo":"04","nombre":"Name","apellidos":"Last","categoria_pobreza":2,"categoriaIcv":1,"nocontrato":"contract"}
""");
var phase1Check = await clientsService.CheckClient("00100118793", null, null, "Salinas");
Check(phase1Check.SiubenPhase == 1 && phase1Check.PobertyLevel == 2 && phase1Check.ContractNumber == "contract", "phase 1 client check mapping");
Check(phase1Check.IsEligible is null && phase1Check.ExistsInSiuben, "phase 1 does not infer eligibility");
factory.Enqueue(HttpStatusCode.OK, """{"razon":"Elegible"}""");
var phase2Check = await clientsService.CheckClient("00100118793", null, null, "Bombita");
Check(phase2Check.SiubenPhase == 2 && phase2Check.IsEligible == true && phase2Check.PobertyLevel is null, "phase 2 client check mapping");
Check(phase2Check.ExistsInWispro && phase2Check.ClientId == "wispro-client", "Wispro check preserved");
using (var json = JsonDocument.Parse(JsonSerializer.Serialize(phase2Check)))
{
    Check(json.RootElement.GetProperty("pobertyLevel").ValueKind == JsonValueKind.Null &&
        json.RootElement.GetProperty("isEligible").GetBoolean(), "phase 2 API serialization");
}
factory.Enqueue(HttpStatusCode.BadRequest, """{"extraInformation":"Persona no elegible"}""");
await Throws<SiubenApiException>(() => clientsService.CheckClient("00100118793", null, null, "Bombita"), "client check propagates business rejection");
var createContext = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
createContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("userId", "1")], "test"));
CreateClientRequest NewClient(string locality) => new()
{
    Name = "Test", Email = "test@example.com", Locality = locality,
    NationalIdentificationNumber = "001-0011879-3", PhoneMobile = "+1 (809) 555-1234", PobertyLevel = 2
};
var detailsRequest = JsonSerializer.Deserialize<CreateClientRequest>("""{"details":"Referencia del hogar\nSegunda línea"}""")!;
using (var detailsJson = JsonDocument.Parse(JsonSerializer.Serialize(detailsRequest.ToWisproClient())))
    Check(detailsJson.RootElement.GetProperty("details").GetString() == "Referencia del hogar\nSegunda línea", "client details preserved from API JSON to Wispro JSON");
Check(NewClient("Bombita").ToWisproClient().Details == string.Empty, "client details remain optional for existing callers");
WisproContract TestContract() => new()
{
    Id = "wispro-contract", PublicId = 987, ClientId = "wispro-client", PlanId = "plan",
    CreatedAt = "2026-09-14T00:00:00Z", State = "enabled"
};
var createRepo = new FakeClients();
var createEmail = new FakeEmail();
var createWispro = new FakeWispro { Contract = TestContract() };
var createService = new ClientsService(new FixedResolver(first, second, localities), createWispro, createRepo,
    createContext, createEmail, localities);
var beforeCreation = factory.Requests.Count;
var created = await createService.CreateClient(NewClient("Bombita"));
Check(created is not null && factory.Requests.Count == beforeCreation && createWispro.ContractLookups == 0,
    "phase 2 creation does not call Siuben or look up a contract");
Check(createRepo.Created.Single().ContractNumber is null && !createRepo.Created.Single().SiubenActivated,
    "created phase 2 client remains pending for scheduled activation");
Check(createEmail.Sent == 1, "creation email preserved");
Check(await createService.CreateClient(NewClient("Bombita")) is null && factory.Requests.Count == beforeCreation, "duplicate creation has no activation side effects");

var clientController = new ClientController(createService, null!, localities);
var storedResult = await clientController.GetClient("001-0011879-3") as OkObjectResult;
Check(ReferenceEquals((storedResult?.Value as ClientDetailsResponse)?.Client, createRepo.Created.Single()), "client endpoint returns the complete stored entity for a formatted document");
Check(await clientController.GetClient("11111111111") is NotFoundObjectResult, "client endpoint returns 404 for missing clients");
Check(typeof(ClientController).GetMethod(nameof(ClientController.GetClient))!.IsDefined(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute)), "client details require authentication");
Check(factory.Requests.Count == beforeCreation, "client detail lookup does not call Siuben");

Check((storedResult?.Value as ClientDetailsResponse)?.WisproContract?.PublicId == 987 && createWispro.ContractLookups > 0,
    "client detail lookup fetches the live Wispro contract");

ScheduledJob Job(FakeWispro wispro, FakeClients repository) => new(wispro, repository,
    new FixedResolver(first, second, localities), localities, settings, NullLogger<ScheduledJob>.Instance);
var job = Job(createWispro, createRepo);
factory.Enqueue(HttpStatusCode.OK, """{"razon":"Habilitado exitosamente para todo el hogar"}""");
await job.Execute(null!);
Check(createRepo.Created.Single().SiubenActivated && createRepo.Created.Single().ContractNumber == "987", "scheduled activation records completion and contract");
Check(createRepo.ActivationUpdates == 1, "scheduled activation marks success once");
using (var json = JsonDocument.Parse(factory.Requests[^1].Body!))
{
    var body = json.RootElement;
    Check(factory.Requests[^1].Path == "/api/data_fase2/HabilitarBeneficiado", "scheduled job calls Siuben enable endpoint");
    Check(body.GetProperty("cedula").GetString() == "00100118793" && body.GetProperty("telefonoBeneficiario").GetString() == "8095551234", "scheduled job normalizes document and phone");
    Check(body.GetProperty("numeroContrato").GetString() == "987" && body.GetProperty("fechaContrato").GetDateTimeOffset() == DateTimeOffset.Parse("2026-09-14T00:00:00Z"), "scheduled job uses actual contract number and date");
    Check(body.GetProperty("prestadora").GetString() == "Exatech", "scheduled job supplies configured provider");
}
var afterActivation = factory.Requests.Count;
await job.Execute(null!);
Check(factory.Requests.Count == afterActivation && createRepo.ActivationUpdates == 1, "subsequent scheduled run skips completed activation");

var noContractRepo = new FakeClients();
var noContractWispro = new FakeWispro();
var noContractService = new ClientsService(new FixedResolver(first, second, localities), noContractWispro, noContractRepo,
    createContext, new FakeEmail(), localities);
await noContractService.CreateClient(NewClient("Bombita"));
var pendingJob = Job(noContractWispro, noContractRepo);
await pendingJob.Execute(null!);
Check(!noContractRepo.Created.Single().SiubenActivated && noContractRepo.ContractUpdates == 0 && factory.Requests.Count == afterActivation,
    "missing contract remains pending without sending fabricated data");
noContractWispro.Contract = TestContract();
factory.Enqueue(HttpStatusCode.OK, """{"razon":"Habilitado"}""");
await pendingJob.Execute(null!);
Check(noContractRepo.Created.Single().SiubenActivated, "later job activates once contract becomes available");

var phase1Wispro = new FakeWispro { Contract = TestContract() };
var phase1Repo = new FakeClients();
var phase1Creation = new ClientsService(new FixedResolver(first, second, localities), phase1Wispro, phase1Repo,
    createContext, new FakeEmail(), localities);
await phase1Creation.CreateClient(NewClient("Salinas"));
var beforePhase1Job = factory.Requests.Count;
await Job(phase1Wispro, phase1Repo).Execute(null!);
Check(phase1Repo.Created.Single().ContractNumber == "987" && phase1Repo.ActivationUpdates == 0 && factory.Requests.Count == beforePhase1Job,
    "phase 1 job synchronizes contract without Siuben activation");

var failedRepo = new FakeClients();
await failedRepo.Create(NewClient("Bombita").ToClient("wispro-client", "1", 123));
// A pre-existing contract number must not be mistaken for completed Siuben activation.
failedRepo.Created.Single().ContractNumber = "987";
var retryJob = Job(new FakeWispro { Contract = TestContract() }, failedRepo);
factory.Enqueue(HttpStatusCode.BadRequest, """{"extraInformation":"Persona no elegible"}""");
await retryJob.Execute(null!);
Check(!failedRepo.Created.Single().SiubenActivated && failedRepo.ActivationUpdates == 0, "failed scheduled activation stays pending");
factory.Enqueue(HttpStatusCode.OK, """{"razon":"Habilitado"}""");
await retryJob.Execute(null!);
Check(failedRepo.Created.Single().SiubenActivated, "failed activation is retried on next job even with a stored contract number");

var batchRepo = new FakeClients();
var badClient = NewClient("Bombita").ToClient("invalid-phone-client", "1", 1);
badClient.PhoneMobile = "invalid";
await batchRepo.Create(badClient);
await batchRepo.Create(NewClient("Bombita").ToClient("valid-client", "1", 2));
factory.Enqueue(HttpStatusCode.OK, """{"razon":"Habilitado"}""");
await Job(new FakeWispro { Contract = TestContract() }, batchRepo).Execute(null!);
Check(!batchRepo.Created[0].SiubenActivated && batchRepo.Created[1].SiubenActivated,
    "one client's failure does not prevent activation of remaining clients");

var disabledContract = TestContract(); disabledContract.State = "disabled";
createWispro.Contract = disabledContract;
factory.Enqueue(HttpStatusCode.OK, """{"razon":"Deshabilitado"}""");
var beforeDisabledLookup = createWispro.ContractLookups;
await job.Execute(null!);
Check(createWispro.ContractLookups > beforeDisabledLookup, "scheduled job rechecks already activated clients");
Check(createRepo.Created.Single().SiubenDisabled && !createRepo.Created.Single().SiubenActivated && createRepo.DeactivationUpdates == 1,
    "disabled Wispro contract deactivates Siuben and records disabled state");
using (var json = JsonDocument.Parse(factory.Requests[^1].Body!))
{
    Check(factory.Requests[^1].Path == "/api/data_fase2/DeshabilitarBeneficiado" && factory.Requests[^1].Method == HttpMethod.Put,
        "scheduled job calls Siuben disable endpoint");
    Check(json.RootElement.GetProperty("cedula").GetString() == "00100118793" &&
        json.RootElement.GetProperty("motivoCancelacion").GetString() == "Cancelaci\u00f3n", "disable payload uses configured cancellation reason and normalized document");
}
var afterDisabled = factory.Requests.Count;
await job.Execute(null!);
Check(factory.Requests.Count == afterDisabled && createRepo.DeactivationUpdates == 1, "successful deactivation is not repeated");
Check(createWispro.LastContractNumber == "987", "scheduled state check targets stored contract number");

var disableRetryRepo = new FakeClients();
var disableRetryClient = NewClient("Bombita").ToClient("wispro-client", "1", 123);
disableRetryClient.ContractNumber = "987"; disableRetryClient.SiubenActivated = true;
await disableRetryRepo.Create(disableRetryClient);
var disableRetryJob = Job(new FakeWispro { Contract = disabledContract }, disableRetryRepo);
factory.Enqueue(HttpStatusCode.InternalServerError, "{}");
await disableRetryJob.Execute(null!);
Check(!disableRetryClient.SiubenDisabled && disableRetryClient.SiubenActivated, "failed deactivation does not mark success");
factory.Enqueue(HttpStatusCode.OK, """{"razon":"Deshabilitado"}""");
await disableRetryJob.Execute(null!);
Check(disableRetryClient.SiubenDisabled && disableRetryRepo.DeactivationUpdates == 1, "failed deactivation is retried next run");

var alreadyDisabledRepo = new FakeClients();
await alreadyDisabledRepo.Create(NewClient("Bombita").ToClient("wispro-client", "1", 123));
factory.Enqueue(HttpStatusCode.BadRequest, """{"extraInformation":"Hogar ya est\u00e1 de baja"}""");
await Job(new FakeWispro { Contract = disabledContract }, alreadyDisabledRepo).Execute(null!);
Check(alreadyDisabledRepo.Created.Single().SiubenDisabled, "already-disabled Siuben response records desired state");
var rejectedDisableRepo = new FakeClients();
await rejectedDisableRepo.Create(NewClient("Bombita").ToClient("wispro-client", "1", 123));
factory.Enqueue(HttpStatusCode.BadRequest, """{"extraInformation":"C\u00e9dula inv\u00e1lida"}""");
await Job(new FakeWispro { Contract = disabledContract }, rejectedDisableRepo).Execute(null!);
Check(!rejectedDisableRepo.Created.Single().SiubenDisabled, "other Siuben 400 errors remain pending");

var beforeDisabledPhase1 = factory.Requests.Count;
phase1Wispro.Contract = disabledContract;
await Job(phase1Wispro, phase1Repo).Execute(null!);
Check(factory.Requests.Count == beforeDisabledPhase1 && phase1Repo.DeactivationUpdates == 0,
    "disabled phase 1 contract never calls phase 2 disable endpoint");
var alertedRepo = new FakeClients();
await alertedRepo.Create(NewClient("Bombita").ToClient("wispro-client", "1", 123));
var alertedContract = TestContract(); alertedContract.State = "alerted";
await Job(new FakeWispro { Contract = alertedContract }, alertedRepo).Execute(null!);
Check(factory.Requests.Count == beforeDisabledPhase1 && !alertedRepo.Created.Single().SiubenActivated && !alertedRepo.Created.Single().SiubenDisabled,
    "alerted contracts do not trigger activation or deactivation");
createWispro.Contract = TestContract();
factory.Enqueue(HttpStatusCode.OK, """{"razon":"Habilitado"}""");
await job.Execute(null!);
Check(createRepo.Created.Single().SiubenActivated && !createRepo.Created.Single().SiubenDisabled,
    "enabled Wispro contract can restore a previously disabled beneficiary");

var invalidContact = NewClient("Bombita"); invalidContact.PhoneMobile = "123";
Check(!validator.Validate(invalidContact).IsValid, "phase 2 creation rejects invalid beneficiary phone");
Check(SiubenPhoneNumber.IsValid(null, "809-555-1234"), "beneficiary phone falls back to fixed phone");

var postFactory = new FakeFactory();
var postProxy = new WisproApiProxy(Options.Create(new AppOptions
{
    WisproClientsUrl = "https://wispro.test/clients", WisproContractsUrl = "https://wispro.test/contracts", WisproApiKey = "test"
}), postFactory, new FakeCache());
postFactory.Enqueue(HttpStatusCode.OK, """{"status":200,"data":{"id":"posted-client","public_id":456}}""");
var postedClient = await postProxy.CreateClient(new WisproClient { Name = "Test & Client", NationalIdentificationNumber = "001-0011879-3" });
Check(postedClient?.Id == "posted-client" && postedClient.PublicId == 456, "Wispro creation returns identifiers from POST response");
Check(postFactory.Requests.Count == 1, "Wispro creation does not depend on immediate search visibility");
Check(Uri.UnescapeDataString(postFactory.Requests[0].Query) == "?name=Test & Client", "Wispro creation escapes the name query parameter");
postFactory.Enqueue(HttpStatusCode.OK, """{"status":200,"data":{"id":"posted-client"}}""");
await Throws<ApiException>(() => postProxy.CreateClient(new WisproClient()), "Wispro creation rejects missing public ID rather than inventing one");
postFactory.Enqueue(HttpStatusCode.OK, """{"status":200,"data":{"id":"","public_id":456}}""");
await Throws<ApiException>(() => postProxy.CreateClient(new WisproClient()), "Wispro creation rejects blank identifiers");
postFactory.Enqueue(HttpStatusCode.BadRequest, "{}");
await Throws<ApiException>(() => postProxy.CreateClient(new WisproClient()), "Wispro creation rejects failed POST without searching");

postFactory.Enqueue(HttpStatusCode.OK, """{"status":400,"message":"Invalid record","errors":{"email":["taken"]}}""");
try
{
    await postProxy.CreateClient(new WisproClient());
    throw new Exception("Expected Wispro business error");
}
catch (ApiException exception)
{
    Check(exception.StatusCode == HttpStatusCode.BadRequest && exception.Errors?.ContainsKey("email") == true && exception.Code == "WISPRO_CREATION_REJECTED",
        "Wispro HTTP 200 business rejection reports status and field instead of claiming creation succeeded");
}
postFactory.Enqueue(HttpStatusCode.OK, """{"status":401,"message":"Unauthorized","errors":{}}""");
await Throws<ApiException>(() => postProxy.CreateClient(new WisproClient()), "Wispro HTTP 200 unauthorized envelope is rejected");
postFactory.Enqueue(HttpStatusCode.OK, """{"data":{"id":"not-confirmed","public_id":456}}""");
await Throws<ApiException>(() => postProxy.CreateClient(new WisproClient()), "Wispro creation requires a successful envelope status");

var recoveryFactory = new FakeFactory();
var recoveryCache = new FakeCache();
await recoveryCache.SetAsync("wispro-00100118793", new WisproClient { Id = "stale", PublicId = 999 }, TimeSpan.FromMinutes(15));
var recoveryProxy = new WisproApiProxy(Options.Create(new AppOptions
{
    WisproClientsUrl = "https://wispro.test/clients", WisproContractsUrl = "https://wispro.test/contracts", WisproApiKey = "test"
}), recoveryFactory, recoveryCache);
WisproClient CreationWithoutIds() => new() { Name = "Test", NationalIdentificationNumber = "001-0011879-3" };
string recoveredData = """{"status":200,"data":[{"id":"recovered","public_id":123,"national_identification_number":"00100118793"}]}""";
recoveryFactory.Enqueue(HttpStatusCode.OK, """{"status":200,"data":{}}""");
recoveryFactory.Enqueue(HttpStatusCode.OK, """{"status":200,"data":[]}""");
recoveryFactory.Enqueue(HttpStatusCode.OK, recoveredData);
Check((await recoveryProxy.CreateClient(CreationWithoutIds()))?.Id == "recovered", "missing POST identifiers recovered by fresh lookup, bypassing stale Redis");
Check(recoveryFactory.Requests.Count(request => request.Method == HttpMethod.Post) == 1 &&
    recoveryFactory.Requests.Count(request => request.Method == HttpMethod.Get) == 2, "delayed visibility retries GET only, never POST");
Check(recoveryFactory.Requests[^1].Query == "?national_identification_number_eq=00100118793", "recovery filters normalized cedula");

foreach (var rows in new[]
{
    """{"status":200,"data":[{"id":"other","public_id":22,"national_identification_number":"99999999999"}]}""",
    """{"status":200,"data":[]}"""
})
{
    var before = recoveryFactory.Requests.Count;
    recoveryFactory.Enqueue(HttpStatusCode.OK, """{"status":200}""");
    for (var attempt = 0; attempt < 3; attempt++) recoveryFactory.Enqueue(HttpStatusCode.OK, rows);
    try { await recoveryProxy.CreateClient(CreationWithoutIds()); throw new Exception("Expected pending identification"); }
    catch (ApiException exception)
    {
        Check(exception.Code == "WISPRO_IDENTIFICATION_PENDING" && exception.StatusCode == HttpStatusCode.Conflict,
            "unresolved lookup explains creation pending without claiming creation failed");
    }
    Check(recoveryFactory.Requests.Count - before == 4, "recovery bounded to one POST and three GETs");
}
recoveryFactory.Enqueue(HttpStatusCode.OK, """{"status":200}""");
recoveryFactory.Enqueue(HttpStatusCode.OK, """{"status":200,"data":[{"id":"one","public_id":1,"national_identification_number":"00100118793"},{"id":"two","public_id":2,"national_identification_number":"00100118793"}]}""");
await Throws<ApiException>(() => recoveryProxy.CreateClient(CreationWithoutIds()), "ambiguous exact document matches never select arbitrary client");
recoveryFactory.Enqueue(HttpStatusCode.OK, """{"status":200,"data":{"id":"expected"}}""");
recoveryFactory.Enqueue(HttpStatusCode.OK, recoveredData);
await Throws<ApiException>(() => recoveryProxy.CreateClient(CreationWithoutIds()), "partial POST ID cannot be replaced by a different client ID");
Check(recoveryFactory.Pending == 0, "recovery consumes only the expected bounded responses");
var recoveredRepository = new FakeClients();
var recoveredService = new ClientsService(new FixedResolver(first, second, localities), recoveryProxy, recoveredRepository,
    createContext, new FakeEmail(), localities);
recoveryFactory.Enqueue(HttpStatusCode.OK, """{"status":200}""");
recoveryFactory.Enqueue(HttpStatusCode.OK, recoveredData);
Check((await recoveredService.CreateClient(NewClient("Bombita")))?.WisproId == "recovered" &&
    recoveredRepository.Created.Single().WisproId == "recovered", "recovered identifiers are persisted and returned through actual creation flow");

var contractFactory = new FakeFactory();
var contractProxy = new WisproApiProxy(Options.Create(new AppOptions
{
    WisproClientsUrl = "https://wispro.test/clients", WisproContractsUrl = "https://wispro.test/contracts", WisproApiKey = "test"
}), contractFactory, cache);
string ContractResponse(IEnumerable<WisproContract> contracts, int status = 200) => JsonSerializer.Serialize(new { status, data = contracts });
contractFactory.Enqueue(HttpStatusCode.OK, ContractResponse([TestContract()]));
Check((await contractProxy.GetContractForClient("001-0011879-3", "wispro-client"))?.PublicId == 987, "Wispro contract lookup returns actual contract");
Check(contractFactory.Requests[^1].Query.Contains("client_national_identification_number_eq=00100118793"), "Wispro lookup uses documented client document filter");
contractFactory.Enqueue(HttpStatusCode.OK, ContractResponse([TestContract()]));
await Throws<InvalidOperationException>(() => contractProxy.GetContractForClient("00100118793", "other-client"), "mismatched client response cannot confirm deletion");
contractFactory.Enqueue(HttpStatusCode.OK, ContractResponse([TestContract(), TestContract()]));
await Throws<InvalidOperationException>(() => contractProxy.GetContractForClient("00100118793", "wispro-client"), "ambiguous contracts rejected");
contractFactory.Enqueue(HttpStatusCode.OK, ContractResponse([], 401));
await Throws<InvalidOperationException>(() => contractProxy.GetContractForClient("00100118793", "wispro-client"), "Wispro envelope error is not treated as missing contract");
contractFactory.Enqueue(HttpStatusCode.OK, ContractResponse([disabledContract]));
var fetchedDisabled = await contractProxy.GetContractForClient("00100118793", "wispro-client", "987");
Check(fetchedDisabled?.State == "disabled" && !contractFactory.Requests[^1].Query.Contains("public_id_eq"),
    "Wispro lookup checks all client contracts before deciding a stored contract was deleted");
contractFactory.Enqueue(HttpStatusCode.OK, ContractResponse([TestContract()]));
Check((await contractProxy.GetContractForClient("00100118793", "wispro-client", "999"))?.PublicId == 987,
    "sole replacement contract prevents treating the client as without a contract");
contractFactory.Enqueue(HttpStatusCode.OK, ContractResponse([]));
Check(await contractProxy.GetContractForClient("00100118793", "wispro-client", "987") is null, "successful empty lookup confirms missing contract");
contractFactory.Enqueue(HttpStatusCode.OK, """{"status":200,"data":null}""");
await Throws<InvalidOperationException>(() => contractProxy.GetContractForClient("00100118793", "wispro-client"), "null response data cannot confirm deletion");
contractFactory.Enqueue(HttpStatusCode.ServiceUnavailable, "{}");
await Throws<HttpRequestException>(() => contractProxy.GetContractForClient("00100118793", "wispro-client"), "Wispro outage cannot confirm deletion");
var tokenClock = new TestClock();
using var expiringCache = new FakeCache(tokenClock);
var expiryFactory = new FakeFactory();
var expiryProxy = new SiubenPhase2ApiProxy(settings, expiryFactory, expiringCache);
expiryFactory.Enqueue(HttpStatusCode.OK, """{"token":"hour-one"}""");
expiryFactory.Enqueue(HttpStatusCode.OK, """{"razon":"Elegible"}""");
await expiryProxy.GetContract("00100118793");
tokenClock.UtcNow += TimeSpan.FromMinutes(59) + TimeSpan.FromSeconds(59);
expiryFactory.Enqueue(HttpStatusCode.OK, """{"razon":"Elegible"}""");
await new SiubenPhase2ApiProxy(settings, expiryFactory, expiringCache).GetContract("00100118793");
Check(expiryFactory.Requests.Count(r => r.Path == "/api/auth/login") == 1 && expiryFactory.Requests[^1].Token == "hour-one",
    "token reused across proxy instances for the full hour");
tokenClock.UtcNow += TimeSpan.FromSeconds(1);
expiryFactory.Enqueue(HttpStatusCode.OK, """{"token":"hour-two"}""");
expiryFactory.Enqueue(HttpStatusCode.OK, """{"razon":"Elegible"}""");
await expiryProxy.GetContract("00100118793");
Check(expiryFactory.Requests.Count(r => r.Path == "/api/auth/login") == 2 && expiryFactory.Requests[^1].Token == "hour-two",
    "token refreshed at one-hour absolute expiration despite recent use");
Check(expiryFactory.Pending == 0, "token expiration calls consumed");

using var concurrentCache = new FakeCache();
var concurrentFactory = new CoordinatedTokenFactory();
var concurrentCalls = Enumerable.Range(0, 8).Select(_ =>
    new SiubenPhase2ApiProxy(settings, concurrentFactory, concurrentCache).GetContract("00100118793")).ToArray();
await concurrentFactory.LoginStarted.Task;
concurrentFactory.ReleaseLogin.SetResult();
await Task.WhenAll(concurrentCalls);
Check(concurrentFactory.Logins == 1 && concurrentFactory.Requests == 8,
    "concurrent proxy instances share one login and cached token");
var database = DispatchProxy.Create<IDatabase, RedisDatabaseStub>();
var redisStore = (RedisDatabaseStub)(object)database;
var redisOptions = Options.Create(new RedisOptions { ConnectionString = "unused-in-mock", InstanceName = "tests:" });
var redisCacheOne = new RedisCacheService(database, redisOptions);
var redisCacheTwo = new RedisCacheService(database, redisOptions);
await redisCacheOne.SetAsync("client", new WisproClient { Id = "shared-client" }, TimeSpan.FromMinutes(15));
Check((await redisCacheTwo.GetAsync<WisproClient>("client"))?.Id == "shared-client", "Redis cache serializes typed objects between instances");
redisStore.Now += TimeSpan.FromMinutes(15);
Check(await redisCacheTwo.GetAsync<WisproClient>("client") is null, "Redis typed cache expires absolutely");
var redisFactory = new CoordinatedTokenFactory();
var sharedRedisCalls = Enumerable.Range(0, 8).Select(i => new SiubenPhase2ApiProxy(settings, redisFactory,
    i % 2 == 0 ? redisCacheOne : redisCacheTwo).GetContract("00100118793")).ToArray();
await redisFactory.LoginStarted.Task;
redisFactory.ReleaseLogin.SetResult();
await Task.WhenAll(sharedRedisCalls);
Check(redisFactory.Logins == 1 && redisFactory.Requests == 8, "Redis lock shares one token refresh across separate cache instances");
Check(redisStore.Lifetimes.Values.Contains(TimeSpan.FromHours(1)), "Redis token lifetime is one hour");
redisStore.Now += TimeSpan.FromMinutes(59) + TimeSpan.FromSeconds(59);
await new SiubenPhase2ApiProxy(settings, redisFactory, redisCacheTwo).GetContract("00100118793");
Check(redisFactory.Logins == 1, "Redis token reused until one-hour expiration");
redisStore.Now += TimeSpan.FromSeconds(1);
await new SiubenPhase2ApiProxy(settings, redisFactory, redisCacheOne).GetContract("00100118793");
Check(redisFactory.Logins == 2, "Redis token refreshed after one hour");
var cachedTokenLoads = 0;
await redisCacheOne.SetAsync("rejected-token", "old", TimeSpan.FromHours(1));
Check(await redisCacheTwo.GetOrCreateAsync("rejected-token", TimeSpan.FromHours(1),
    () => { cachedTokenLoads++; return Task.FromResult("new"); }, token => token != "old") == "new" && cachedTokenLoads == 1,
    "Redis cache replaces rejected tokens");
redisStore.LoseNextLock = true;
await Throws<InvalidOperationException>(() => redisCacheOne.GetOrCreateAsync("lost-lock", TimeSpan.FromHours(1),
    () => Task.FromResult("stale")), "expired Redis lock owner cannot publish a stale token");
Check(await redisCacheTwo.GetAsync<string>("lost-lock") is null, "lost-lock value was not cached");
redisStore.FailReads = true;
await Throws<InvalidOperationException>(() => redisCacheOne.GetOrCreateAsync<string>("outage", TimeSpan.FromHours(1),
    () => throw new Exception("Must not log in during Redis outage")), "Redis outage propagates without uncached login fallback");
redisStore.FailReads = false;
await Throws<InvalidOperationException>(() => redisCacheOne.GetOrCreateAsync<string>("failed-loader", TimeSpan.FromHours(1),
    () => throw new InvalidOperationException("Login failed")), "failed login releases Redis refresh lock");
Check(await redisCacheTwo.GetOrCreateAsync("failed-loader", TimeSpan.FromHours(1), () => Task.FromResult("recovered")) == "recovered",
    "another cache instance can refresh after login failure");

var liveRedisConnection = Environment.GetEnvironmentVariable("REDIS_TEST_CONNECTION");
if (!string.IsNullOrWhiteSpace(liveRedisConnection))
{
    using var connection = await ConnectionMultiplexer.ConnectAsync(liveRedisConnection);
    var liveOptions = Options.Create(new RedisOptions { InstanceName = $"indotel-checks:{Guid.NewGuid():N}:" });
    var liveOne = new RedisCacheService(connection.GetDatabase(), liveOptions);
    var liveTwo = new RedisCacheService(connection.GetDatabase(), liveOptions);
    await liveOne.SetAsync("live-expiry", "value", TimeSpan.FromMilliseconds(200));
    Check(await liveTwo.GetAsync<string>("live-expiry") == "value", "live Redis cross-instance read");
    await Task.Delay(300);
    Check(await liveTwo.GetAsync<string>("live-expiry") is null, "live Redis absolute expiration");
    var loads = 0;
    await Task.WhenAll(Enumerable.Range(0, 8).Select(i => (i % 2 == 0 ? liveOne : liveTwo)
        .GetOrCreateAsync("live-refresh", TimeSpan.FromSeconds(5), async () =>
        {
            Interlocked.Increment(ref loads);
            await Task.Delay(100);
            return "token";
        })));
    Check(loads == 1, "live Redis distributed refresh lock");
}
Check(typeof(SiubenController).GetMethod(nameof(SiubenController.Synchronize))!
    .GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Any(attribute => attribute.Roles == "Admin"),
    "synchronization requires Admin");
var manualRepository = new FakeClients();
var manualClient = NewClient("Bombita").ToClient("wispro-client", "1", 123);
manualClient.ContractNumber = "987";
await manualRepository.Create(manualClient);
var manualWispro = new FakeWispro { Contract = TestContract() };
var manualService = new ClientsService(new FixedResolver(first, second, localities), manualWispro, manualRepository,
    createContext, createEmail, localities);
var manual = new SiubenController(new FixedResolver(first, second, localities), localities, manualRepository);
SynchronizeBeneficiaryRequest SyncRequest() => new() { DocumentNumber = "00100118793", CancellationReason = "Cancelación" };
var beforeManualGuard = factory.Requests.Count;
manualWispro.Contract = null;
manualClient.ContractNumber = null;
var missingDetails = await manualService.GetByDocumentNumber("001-0011879-3");
Check(missingDetails is { WisproContract: null, CanSynchronize: false } && missingDetails.Client == manualClient,
    "lookup preserves client details but blocks clients without Wispro contracts");
Check(await manual.Synchronize(SyncRequest(), manualService, settings) is ConflictObjectResult,
    "synchronization rejects missing contracts");
manualWispro.Contract = alertedContract;
Check(await manual.Synchronize(SyncRequest(), manualService, settings) is ConflictObjectResult,
    "synchronization rejects alerted Wispro contracts");
Check(factory.Requests.Count == beforeManualGuard && manualRepository.ActivationUpdates == 0 && manualRepository.DeactivationUpdates == 0,
    "blocked synchronization makes no Siuben call or local state change");
manualClient.ContractNumber = "987";
manualWispro.Contract = TestContract();
manualClient.Locality = "Salinas";
Check(await manual.Synchronize(SyncRequest(), manualService, settings) is BadRequestObjectResult,
    "synchronization rejects phase 1 stored locality");
manualClient.Locality = "Bombita";
factory.Enqueue(HttpStatusCode.OK, """{"razon":"Habilitado"}""");
Check(await manual.Synchronize(SyncRequest(), manualService, settings) is OkObjectResult && manualClient.SiubenActivated,
    "synchronization derives enable from live Wispro state");
using (var storedBody = JsonDocument.Parse(factory.Requests[^1].Body!))
{
    Check(factory.Requests[^1].Path.EndsWith("/HabilitarBeneficiado") &&
        storedBody.RootElement.GetProperty("numeroContrato").GetString() == "987" &&
        storedBody.RootElement.GetProperty("fechaContrato").GetDateTimeOffset() == DateTimeOffset.Parse("2026-09-14T00:00:00Z"),
        "synchronization uses actual Wispro contract number and date");
}
// The browser could have looked up enabled; the next request must still use the new disabled state.
manualWispro.Contract = disabledContract;
var invalidReason = SyncRequest(); invalidReason.CancellationReason = "Otro";
Check(await manual.Synchronize(invalidReason, manualService, settings) is BadRequestObjectResult,
    "disabled synchronization validates cancellation description");
factory.Enqueue(HttpStatusCode.OK, """{"razon":"Deshabilitado"}""");
invalidReason.OtherCancellationReason = "Cambio solicitado";
Check(await manual.Synchronize(invalidReason, manualService, settings) is OkObjectResult && manualClient.SiubenDisabled && !manualClient.SiubenActivated,
    "synchronization rechecks Wispro and derives disable from the updated state");
using (var disabledBody = JsonDocument.Parse(factory.Requests[^1].Body!))
    Check(factory.Requests[^1].Path.EndsWith("/DeshabilitarBeneficiado") &&
        disabledBody.RootElement.GetProperty("otroMotivoCancelacion").GetString() == "Cambio solicitado",
        "synchronization preserves editable cancellation reason");
var beforeRepeatedSync = factory.Requests.Count;
Check((await manualService.GetByDocumentNumber("00100118793")) is { AlreadySynchronized: true, CanSynchronize: false },
    "matching disabled state is already synchronized");
Check(await manual.Synchronize(SyncRequest(), manualService, settings) is ConflictObjectResult && factory.Requests.Count == beforeRepeatedSync,
    "matching state blocks repeated synchronization without a Siuben call");
manualWispro.Contract = TestContract();
manualClient.SiubenActivated = true; manualClient.SiubenDisabled = false;
Check((await manualService.GetByDocumentNumber("00100118793")) is { AlreadySynchronized: true, CanSynchronize: false },
    "matching enabled state is already synchronized");
manualClient.SiubenActivated = false; manualClient.SiubenDisabled = true;
Check((await manualService.GetByDocumentNumber("00100118793")) is { AlreadySynchronized: false, CanSynchronize: true },
    "changed Wispro state allows synchronization again");
manualWispro.Contract = disabledContract;
manualClient.SiubenActivated = true; manualClient.SiubenDisabled = false;
var successfulManualUpdates = manualRepository.DeactivationUpdates;
factory.Enqueue(HttpStatusCode.InternalServerError, "{}");
await Throws<SiubenApiException>(() => manual.Synchronize(SyncRequest(), manualService, settings), "synchronization propagates Siuben errors");
Check(manualRepository.DeactivationUpdates == successfulManualUpdates, "failed synchronization does not persist success");
manualWispro.Contract = null;
Check((await manualService.GetByDocumentNumber("00100118793")) is { ContractDeleted: true, CanSynchronize: true },
    "previously linked contract absent from Wispro allows cancellation");
factory.Enqueue(HttpStatusCode.OK, """{"razon":"Deshabilitado por contrato eliminado"}""");
Check(await manual.Synchronize(SyncRequest(), manualService, settings) is OkObjectResult && manualClient.SiubenDisabled && !manualClient.SiubenActivated,
    "manual synchronization disables beneficiary for deleted contract");
Check(manualClient.ContractNumber == "987", "deleted contract number retained for history");
var afterDeletedManual = factory.Requests.Count;
Check((await manualService.GetByDocumentNumber("00100118793")) is { ContractDeleted: true, AlreadySynchronized: true, CanSynchronize: false },
    "deleted contract with cancelled beneficiary is already synchronized");
Check(await manual.Synchronize(SyncRequest(), manualService, settings) is ConflictObjectResult && factory.Requests.Count == afterDeletedManual,
    "deleted contract is not cancelled twice manually");
var deletedRepo = new FakeClients();
var deletedClient = NewClient("Bombita").ToClient("wispro-client", "1", 123);
deletedClient.ContractNumber = "987"; deletedClient.SiubenActivated = true;
await deletedRepo.Create(deletedClient);
var deletedJob = Job(new FakeWispro(), deletedRepo);
factory.Enqueue(HttpStatusCode.InternalServerError, "{}");
await deletedJob.Execute(null!);
Check(deletedRepo.DeactivationUpdates == 0 && deletedClient.SiubenActivated, "failed cancellation for deletion remains pending");
factory.Enqueue(HttpStatusCode.OK, """{"razon":"Deshabilitado"}""");
await deletedJob.Execute(null!);
Check(deletedRepo.DeactivationUpdates == 1 && deletedClient.SiubenDisabled, "job disables deleted contract on retry");
var afterDeletedJob = factory.Requests.Count;
await deletedJob.Execute(null!);
Check(factory.Requests.Count == afterDeletedJob && deletedRepo.DeactivationUpdates == 1, "job skips already cancelled deleted contracts");
await Job(new FakeWispro(), phase1Repo).Execute(null!);
Check(factory.Requests.Count == afterDeletedJob, "deleted phase 1 contract does not call phase 2 cancellation");
Check(factory.Pending == 0, "all mocked calls consumed");
Console.WriteLine($"Passed {count} Siuben checks.");

record Captured(HttpMethod Method, string Path, string? Token, string? Body, string Query);
sealed class FakeFactory : HttpMessageHandler, IHttpClientFactory
{
    private readonly Queue<(HttpStatusCode Status, string Body)> _responses = new();
    public List<Captured> Requests { get; } = [];
    public int Pending => _responses.Count;
    public void Enqueue(HttpStatusCode status, string body) => _responses.Enqueue((status, body));
    public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Requests.Add(new(request.Method, request.RequestUri!.AbsolutePath, request.Headers.Authorization?.Parameter,
            request.Content is null ? null : await request.Content.ReadAsStringAsync(token), request.RequestUri.Query));
        if (_responses.Count == 0) throw new Exception($"Unexpected HTTP call: {request.RequestUri}");
        var next = _responses.Dequeue();
        return new(next.Status) { Content = new StringContent(next.Body, Encoding.UTF8, "application/json") };
    }
}

sealed class FixedResolver(ISiubenApiProxy phase1, ISiubenApiProxy phase2, SiubenLocalities localities) : ISiubenApiProxyResolver
{
    public ISiubenApiProxy Resolve(string locality) => localities.GetPhase(locality) == SiubenPhase.Phase1 ? phase1 : phase2;
}
sealed class FakeWispro : IWisproApiProxy
{
    public WisproContract? Contract { get; set; }
    public int ContractLookups { get; private set; }
    public string? LastContractNumber { get; private set; }
    public Task<WisproContract?> GetContractForClient(string documentNumber, string clientId, string? contractNumber = null)
    {
        ContractLookups++;
        LastContractNumber = contractNumber;
        return Task.FromResult(Contract);
    }
    public Task<WisproClient?> GetClient(string? documentNumber) => Task.FromResult<WisproClient?>(new() { Id = "wispro-client" });
    public Task<WisproClient?> CreateClient(WisproClient client) => Task.FromResult<WisproClient?>(new() { Id = "wispro-client", PublicId = 123 });
    public Task<IEnumerable<WisproContract>> GetContracsByDateRange(DateTime startDate, DateTime endDate) => throw new NotSupportedException();
}

sealed class FakeClients : IClientRepository
{
    public List<Client> Created { get; } = [];
    public int ContractUpdates { get; private set; }
    public Task Create(Client client) { Created.Add(client); return Task.CompletedTask; }
    public Task<bool> Exists(string document) => Task.FromResult(Created.Any(c => c.NationalIdentificationNumber == document));
    public Task AddContractNumber(string wisproId, string contractNumber)
    {
        ContractUpdates++;
        Created.Single(c => c.WisproId == wisproId).ContractNumber = contractNumber;
        return Task.CompletedTask;
    }
    public int DeactivationUpdates { get; private set; }
    public Task MarkSiubenDisabled(string wisproId, string contractNumber)
    {
        DeactivationUpdates++;
        var client = Created.Single(c => c.WisproId == wisproId);
        client.ContractNumber = contractNumber;
        client.SiubenActivated = false;
        client.SiubenDisabled = true;
        return Task.CompletedTask;
    }
    public int ActivationUpdates { get; private set; }
    public Task MarkSiubenActivated(string wisproId, string contractNumber)
    {
        ActivationUpdates++;
        var client = Created.Single(c => c.WisproId == wisproId);
        client.ContractNumber = contractNumber;
        client.SiubenActivated = true;
        client.SiubenDisabled = false;
        return Task.CompletedTask;
    }
    public Task<Client?> GetByDocumentNumber(string documentNumber) => Task.FromResult(Created.FirstOrDefault(c =>
        c.NationalIdentificationNumber.Replace("-", string.Empty) == documentNumber.Replace("-", string.Empty)));
    public Task<Client?> GetByWisproId(string id) => Task.FromResult(Created.FirstOrDefault(c => c.WisproId == id));
    public Task<Client?> GetByEmail(string email) => Task.FromResult(Created.FirstOrDefault(c => c.Email == email));
    public Task<IEnumerable<Client>> GetClientsForContractSynchronization() => Task.FromResult<IEnumerable<Client>>(Created.ToArray());
}
sealed class FakeEmail : IEmailSenderService
{
    public int Sent { get; private set; }
    public Task SendEmail(Client client, EventType eventType, IEnumerable<string>? copies = null) { Sent++; return Task.CompletedTask; }
    public Task SendEmail<T>(T data, EventType eventType, Func<EmailTemplate, T, string> fillTemplate, IEnumerable<string>? copies = null) => throw new NotSupportedException();
}

sealed class TestClock
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
}
sealed class CoordinatedTokenFactory : HttpMessageHandler, IHttpClientFactory
{
    public TaskCompletionSource LoginStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseLogin { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Logins;
    public int Requests;
    public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri!.AbsolutePath == "/api/auth/login")
        {
            Interlocked.Increment(ref Logins);
            LoginStarted.TrySetResult();
            await ReleaseLogin.Task;
            return new(HttpStatusCode.OK) { Content = new StringContent("""{"token":"shared"}""", Encoding.UTF8, "application/json") };
        }
        Interlocked.Increment(ref Requests);
        if (request.Headers.Authorization?.Parameter != "shared") throw new Exception("Unexpected shared token");
        return new(HttpStatusCode.OK) { Content = new StringContent("""{"razon":"Elegible"}""", Encoding.UTF8, "application/json") };
    }
}

sealed class FakeCache(TestClock? clock = null) : ICacheService, IDisposable
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Json, DateTimeOffset Expires)> _values = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset Now => clock?.UtcNow ?? DateTimeOffset.UtcNow;
    public Task<T?> GetAsync<T>(string key) where T : class
    {
        var value = _values.TryGetValue(key, out var entry) && entry.Expires > Now
            ? JsonSerializer.Deserialize<T>(entry.Json) : null;
        return Task.FromResult(value);
    }
    public Task SetAsync<T>(string key, T value, TimeSpan lifetime) where T : class
    {
        _values[key] = (JsonSerializer.Serialize(value), Now + lifetime);
        return Task.CompletedTask;
    }
    public async Task<T> GetOrCreateAsync<T>(string key, TimeSpan lifetime, Func<Task<T>> create, Func<T, bool>? isValid = null) where T : class
    {
        await _gate.WaitAsync();
        try
        {
            var cached = await GetAsync<T>(key);
            if (cached is not null && (isValid is null || isValid(cached))) return cached;
            var value = await create();
            await SetAsync(key, value, lifetime);
            return value;
        }
        finally { _gate.Release(); }
    }
    public void Dispose() => _gate.Dispose();
}
