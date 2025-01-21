using Exatech_Indotel_API.Models.User;
using Exatech_Indotel_API.Repositories.UserRepository;
using Exatech_Indotel_API.Services.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Exatech_Indotel_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userRepository)
        {
            _userService = userRepository;
        }

        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest user)
        {
            await _userService.CreateUser(user);

            return Ok();
        }
    }
}
