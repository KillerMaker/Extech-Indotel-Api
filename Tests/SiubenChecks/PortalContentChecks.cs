using System.Security.Claims;
using Exatech_Indotel_API.Controllers;
using Exatech_Indotel_API.Models.PortalContent;
using Exatech_Indotel_API.Repositories.PortalContentRepository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

public static class PortalContentChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        var repository = new FakeContent();
        repository.Values["unknown"] = "Not published";
        var controller = new PortalContentController(repository);
        var initial = (Dictionary<string, string>)((OkObjectResult)await controller.Get()).Value!;
        check(initial["login.title"] == "Qué bueno tenerte aquí." && !initial.ContainsKey("unknown"), "public content returns only approved fields with defaults");
        foreach (var method in new[] { "Settings", "Update" })
            check(typeof(PortalContentController).GetMethod(method)!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>().Single().Roles == "Admin", "content " + method + " requires Admin on the API");
        check(typeof(PortalContentController).GetMethod("Get")!.IsDefined(typeof(AllowAnonymousAttribute), true), "public login text is readable without authentication");
        check(await controller.Update(new() { ["unknown"] = "test" }) is BadRequestObjectResult && repository.Writes == 0, "unknown content keys cannot be persisted");
        check(await controller.Update(new() { ["login.title"] = " " }) is BadRequestObjectResult && repository.Writes == 0, "blank messages rejected before persistence");
        check(await controller.Update(new() { ["login.title"] = new string('a', 201) }) is BadRequestObjectResult && repository.Writes == 0, "oversized content rejected before persistence");
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        check(await controller.Update(new() { ["login.title"] = "Hola" }) is ForbidResult && repository.Writes == 0, "content edits require a valid user identity for attribution");
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("userId", "8"), new Claim(ClaimTypes.Role, "Admin")], "test"));
        await controller.Update(new() { ["login.headline"] = "  Conectamos\nOportunidades  " });
        var applied = (Dictionary<string, string>)((OkObjectResult)await controller.Get()).Value!;
        check(applied["login.headline"] == "Conectamos\nOportunidades" && repository.UserId == 8 && repository.Writes == 1, "content changes preserve line breaks and are attributed to the administrator");
        check(applied["login.title"] == initial["login.title"], "partial updates preserve unrelated messages");
        check(PortalContentCatalog.Fields.Select(field => field.Key).Distinct().Count() == PortalContentCatalog.Fields.Count &&
            PortalContentCatalog.Fields.All(field => field.DefaultValue.Length <= field.MaxLength), "catalog keys are unique and defaults respect validation limits");
    }

    private sealed class FakeContent : IPortalContentRepository
    {
        public Dictionary<string, string> Values { get; } = new();
        public int Writes { get; private set; }
        public int UserId { get; private set; }
        public Task<IReadOnlyDictionary<string, string>> GetAll() => Task.FromResult<IReadOnlyDictionary<string, string>>(Values);
        public Task Save(IReadOnlyDictionary<string, string> messages, int userId)
        {
            foreach (var item in messages) Values[item.Key] = item.Value;
            Writes++; UserId = userId; return Task.CompletedTask;
        }
    }
}
