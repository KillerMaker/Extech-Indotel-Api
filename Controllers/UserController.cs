using Exatech_Indotel_API.Models.User;
using Exatech_Indotel_API.Repositories.UserRepository;
using Exatech_Indotel_API.Services.User;
using FluentValidation;
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
        private readonly IValidator<CreateUserRequest> _validator;

        public UserController(IUserService userRepository, IValidator<CreateUserRequest>validator)
        {
            _userService = userRepository;
            _validator = validator;
        }

        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest user)
        {
            var validationResult = _validator.Validate(user);

            if (validationResult.IsValid is false)
                return BadRequest(new { errors = validationResult.Errors });

            await _userService.CreateUser(user);

            return Ok();
        }
    }
}
