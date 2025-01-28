using Exatech_Indotel_API.Models.Client;
using Exatech_Indotel_API.Services.Clients;
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
        private readonly IClientsService _clientsService; 
        private readonly IValidator<CreateClientRequest> _validator;

        public ClientController(IClientsService clientsService, IValidator<CreateClientRequest>validator)
        {
            _clientsService = clientsService;
            _validator = validator;
        }

        [Authorize]
        [HttpGet("check")]
        public async Task<IActionResult> CheckClient([FromQuery] string documentNumber, [FromQuery] string? phoneNumber, [FromQuery] string? email)
        {
            var response = await _clientsService.CheckClient(documentNumber, phoneNumber, email);

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
                return BadRequest(new {errorMessage = "Cliente ya existe en la base de datos"});

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
