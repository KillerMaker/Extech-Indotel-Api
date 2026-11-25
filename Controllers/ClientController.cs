using Exatech_Indotel_API.Models.Client;
using Exatech_Indotel_API.Services.Clients;
using Exatech_Indotel_API.Services.Siuben;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Exatech_Indotel_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClientController : ControllerBase
    {
        private readonly SiubenLocalities _localities;
        private readonly IClientsService _clientsService; 
        private readonly IValidator<CreateClientRequest> _validator;

        public ClientController(IClientsService clientsService, IValidator<CreateClientRequest>validator, SiubenLocalities localities)
        {
            _localities = localities;
            _clientsService = clientsService;
            _validator = validator;
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("list")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> GetClients([FromQuery] ClientListRequest request) =>
            Ok(await _clientsService.GetPage(request));

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetClient([FromQuery, Required, RegularExpression(@"^(?:[0-9]{11}|[0-9]{3}-[0-9]{7}-[0-9])$")] string documentNumber)
        {
            var client = await _clientsService.GetByDocumentNumber(documentNumber);

            if (client is null)
                return NotFound(new { errorMessage = "Cliente no encontrado en el repositorio de clientes." });

            return Ok(client);
        }

        [Authorize]
        [HttpGet("check")]
        public async Task<IActionResult> CheckClient([FromQuery, Required, RegularExpression(@"^(?:[0-9]{11}|[0-9]{3}-[0-9]{7}-[0-9])$")] string documentNumber, [FromQuery, Required] string locality, [FromQuery] string? phoneNumber, [FromQuery] string? email)
        {
            if (!_localities.IsSupported(locality))
                return BadRequest(new { errorMessage = "Locality must be a supported Siuben locality." });

            var response = await _clientsService.CheckClient(documentNumber, phoneNumber, email, locality);

            return Ok(response);
        }

        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> CreateClient([FromBody] CreateClientRequest request)
        {
            var validationResult = _validator.Validate(request);

            if (validationResult.IsValid is false)
                return BadRequest(new { errors = validationResult.Errors });

            var response = await _clientsService.CreateClient(request);

            if(response is null)
                return Conflict(new
                {
                    title = "Cliente ya registrado",
                    message = "Ya existe un cliente con esta cédula en el portal. Consulte su registro en lugar de crearlo nuevamente.",
                    code = "CLIENT_ALREADY_EXISTS",
                    traceId = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier
                });

            return Ok(response);
        }

        //[Authorize]
        //[HttpPut("change-contract")]
        //public async Task<IActionResult> UpdateClient([FromBody] ClientUpdateDto request)
        //{
        //    var response = await _clientsService.UpdateClient(request);

        //    if(string.IsNullOrEmpty(response))
        //        return BadRequest(new {errorMessage = "Cliente no existe en la base de datos"});

        //    return Ok( new { clientId = response });
        //}
    }
}
