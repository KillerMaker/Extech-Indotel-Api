using System.Text.Json;
using System.Text.Json.Nodes;

namespace Exatech_Indotel_API.Simulation;

internal static class MockServices
{
    public static WebApplication Create()
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Simulation",
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.WebHost.UseUrls("http://127.0.0.1:5199");
        var app = builder.Build();
        var state = new MockState();
        app.MapMethods("/{**path}", ["GET", "POST", "PUT"], async context =>
        {
            JsonObject body;
            try
            {
                body = context.Request.Method == "GET" || context.Request.ContentLength == 0
                    ? new JsonObject()
                    : await JsonNode.ParseAsync(context.Request.Body) as JsonObject ?? new JsonObject();
            }
            catch (JsonException) { context.Response.StatusCode = 400; return; }
            IResult result;
            lock (state)
            {
                try { result = state.Route(context.Request, body); }
                catch (Exception error) when (error is InvalidOperationException or FormatException or KeyNotFoundException)
                {
                    result = Results.BadRequest(new { message = error.Message });
                }
            }
            await result.ExecuteAsync(context);
        });
        return app;
    }
}

internal sealed class MockState
{
    private readonly Dictionary<string, JsonObject> clients = new();
    private readonly Dictionary<string, JsonObject> contracts = new();
    private readonly Dictionary<string, bool> beneficiaries = new();
    private readonly Dictionary<string, DateTimeOffset> tokens = new();
    private readonly List<object> calls = new();

    public MockState()
    {
        foreach (var seed in SimulationFixtures.Clients)
        {
            var doc = seed.Document;
            clients[doc] = new JsonObject { ["id"] = "sim-" + doc, ["public_id"] = 1000 + seed.Id,
                ["name"] = "Cliente simulado " + seed.Id, ["national_identification_number"] = doc };
            if (seed.HasContract) contracts[doc] = Contract(doc, seed.WisproState!, 980 + seed.Id);
            beneficiaries[doc] = seed.Activated;
        }
    }

    private JsonObject Contract(string doc, string state, int number) => new()
    {
        ["id"] = "contract-" + doc, ["public_id"] = number, ["client_id"] = clients[doc]["id"]!.GetValue<string>(),
        ["plan_id"] = "sim-plan", ["created_at"] = "2026-09-14T00:00:00Z", ["state"] = state
    };
    private static string Text(JsonObject body, string name) => body[name]?.GetValue<string>() ?? "";
    // Serialize under the state lock so subsequent requests cannot mutate a pending response.
    private static IResult Reply(object body, int status = 200) => Results.Text(JsonSerializer.Serialize(body), "application/json", statusCode: status);

    public IResult Route(HttpRequest request, JsonObject body)
    {
        var path = request.Path.Value ?? "";
        var method = request.Method;
        if (path == "/health") return Reply(new { service = "Simulated Siuben and Wispro" });
        if (path == "/control/state" && method == "GET") return Reply(new { clients, contracts, beneficiaries, calls });
        if (path == "/control/contract" && method == "PUT")
        {
            var doc = Text(body, "documentNumber");
            if (!clients.ContainsKey(doc)) return Reply(new { message = "Simulated client not found" }, 404);
            var state = Text(body, "state");
            if (state is not ("enabled" or "disabled" or "alerted")) return Reply(new { message = "Invalid Wispro state" }, 400);
            var number = body["contractNumber"]?.GetValue<int>() ?? contracts.GetValueOrDefault(doc)?["public_id"]?.GetValue<int>() ?? 2000;
            contracts[doc] = Contract(doc, state, number);
            return Reply(contracts[doc]);
        }

        calls.Add(new { method, path, time = DateTimeOffset.UtcNow });
        if (path == "/api/auth/login" && method == "POST")
        {
            var name = Text(body, "name");
            if (name is not ("phase1" or "phase2") || Text(body, "password") != "simulation") return Reply(new { }, 401);
            var token = name + "-" + Guid.NewGuid().ToString("N");
            tokens[token] = DateTimeOffset.UtcNow.AddHours(1);
            return Reply(new { token });
        }
        if (path.StartsWith("/api/data", StringComparison.OrdinalIgnoreCase))
        {
            var token = request.Headers.Authorization.ToString();
            if (!token.StartsWith("Bearer ", StringComparison.Ordinal) ||
                !tokens.TryGetValue(token[7..], out var expiry) || expiry <= DateTimeOffset.UtcNow) return Reply(new { }, 401);
            var doc = method == "GET" || path.Contains("/update/") ? path.Split('/')[^1] : Text(body, "cedula");
            if (doc == "11111111111") return Reply(new { extraInformation = "Persona no encontrada" }, 404);
            if (path.StartsWith("/api/data_fase2/get/") && method == "GET")
                return doc == "22222222222" ? Reply(new { extraInformation = "Persona no elegible - otra prestadora" }, 400) : Reply(new { razon = "Elegible" });
            if (path.StartsWith("/api/Data/get/") && method == "GET")
                return Reply(new { idHogar = "SIM-HOME", cedula = doc, provincia = "Barahona", provinciaCodigo = "04",
                    nombre = "Persona", apellidos = "Simulada", categoria_pobreza = 2, categoriaIcv = 2, nocontrato = (string?)null });
            if (path.StartsWith("/api/Data/update/") && method == "PUT") return Reply(new { razon = "Contrato actualizado" });
            if (path.EndsWith("/HabilitarBeneficiado") && method == "PUT")
            {
                if (new[] { "cedula", "telefonoBeneficiario", "prestadora", "numeroContrato", "fechaContrato" }.Any(key => string.IsNullOrWhiteSpace(Text(body, key))))
                    return Reply(new { message = "Incomplete activation payload" }, 400);
                beneficiaries[doc] = true;
                return Reply(new { razon = "Habilitado exitosamente (simulacion)" });
            }
            if (path.EndsWith("/DeshabilitarBeneficiado") && method == "PUT")
            {
                if (string.IsNullOrWhiteSpace(Text(body, "motivoCancelacion"))) return Reply(new { message = "Missing cancellation reason" }, 400);
                beneficiaries[doc] = false;
                return Reply(new { razon = "Deshabilitado exitosamente (simulacion)" });
            }
        }
        if (path.StartsWith("/api/v1/"))
        {
            if (request.Headers.Authorization != "simulation-wispro") return Reply(new { }, 401);
            if (path == "/api/v1/clients")
            {
                if (method == "POST")
                {
                    var doc = Text(body, "national_identification_number");
                    if (clients.ContainsKey(doc)) return Reply(new { message = "Duplicate client" }, 409);
                    body["id"] = "sim-" + doc; body["public_id"] = 1001 + clients.Count;
                    clients[doc] = body;
                    return Reply(new { status = 200, data = body });
                }
                if (method == "GET")
                {
                    var doc = request.Query["national_identification_number_eq"].ToString();
                    return Reply(new { status = 200, data = clients.TryGetValue(doc, out var client) ? new[] { client } : Array.Empty<JsonObject>() });
                }
            }
            if (path == "/api/v1/contracts" && method == "GET")
            {
                var doc = request.Query["client_national_identification_number_eq"].ToString();
                IEnumerable<JsonObject> rows = doc.Length == 0 ? contracts.Values : contracts.TryGetValue(doc, out var contract) ? [contract] : [];
                var number = request.Query["public_id_eq"].ToString();
                if (number.Length > 0) rows = rows.Where(row => row["public_id"]!.ToString() == number);
                return Reply(new { status = 200, data = rows.ToArray(), meta = new { pagination = new { total_pages = 1 } } });
            }
        }
        return Reply(new { message = "No simulated route" }, 404);
    }
}
