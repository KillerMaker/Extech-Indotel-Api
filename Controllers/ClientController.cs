using Exatech_Indotel_API.Models.Client;
using Exatech_Indotel_API.Services.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Exatech_Indotel_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClientController : ControllerBase
    {
        private readonly IClientsService _clientsService; 
        public ClientController(IClientsService clientsService)
        {
            _clientsService = clientsService;
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
        public async Task<IActionResult> CreateClient([FromBody] ClientCreateDto request)
        {
            var response = await _clientsService.CreateClient(request);

            return Ok(response);
        }
    }
}
