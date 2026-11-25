using Exatech_Indotel_API.Models.PortalContent;
using Exatech_Indotel_API.Repositories.PortalContentRepository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exatech_Indotel_API.Controllers;

[ApiController]
[Route("api/portal-content")]
public class PortalContentController(IPortalContentRepository repository) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get()
    {
        var saved = await repository.GetAll();
        return Ok(PortalContentCatalog.Fields.ToDictionary(field => field.Key,
            field => saved.GetValueOrDefault(field.Key, field.DefaultValue)));
    }

    [HttpGet("settings")]
    [Authorize(Roles = "Admin")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Settings()
    {
        var saved = await repository.GetAll();
        return Ok(PortalContentCatalog.Fields.Select(field => new
        {
            field.Key, field.Label, field.Group, field.DefaultValue, field.MaxLength,
            value = saved.GetValueOrDefault(field.Key, field.DefaultValue)
        }));
    }

    [HttpPut]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update([FromBody] Dictionary<string, string?> messages)
    {
        var errors = new Dictionary<string, string[]>();
        if (messages.Count == 0 || messages.Count > PortalContentCatalog.Fields.Count)
            return BadRequest(new { message = "Seleccione al menos un texto válido para guardar." });
        foreach (var message in messages)
        {
            var field = PortalContentCatalog.Fields.FirstOrDefault(item => item.Key == message.Key);
            if (field is null) errors[message.Key] = ["Este texto no se puede configurar."];
            else if (string.IsNullOrWhiteSpace(message.Value) || message.Value.Trim().Length > field.MaxLength)
                errors[message.Key] = [$"{field.Label}: escriba un texto de entre 1 y {field.MaxLength} caracteres."];
        }
        if (errors.Count > 0) return BadRequest(new { message = "Revise los textos indicados.", errors });
        if (!int.TryParse(User.FindFirst("userId")?.Value, out var userId) || userId <= 0) return Forbid();
        await repository.Save(messages.ToDictionary(item => item.Key, item => item.Value!.Trim()), userId);
        return await Get();
    }
}
