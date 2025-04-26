using Docplanner.Common.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Docplanner.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly List<UserCredentialDto> _userCredentials;
        private readonly ITokenService _tokenService;
        private readonly IConfiguration _config;

        public AuthController(ITokenService tokenService, IConfiguration config, List<UserCredentialDto> userCredentials)
        {
            _tokenService = tokenService;
            _config = config;
            _userCredentials = userCredentials;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginDto login)
        {
            if (string.IsNullOrWhiteSpace(login.Username) || string.IsNullOrWhiteSpace(login.Password))
            {
                return Unauthorized(new { Message = "Invalid credentials" });
            }

            var hashedInput = PasswordHasher.Hash(login.Password);
            var match = _userCredentials.FirstOrDefault(u => u.Username == login.Username && u.Password == hashedInput);

            if (match == null)
            {
                return Unauthorized(new { Message = "Invalid credentials" });
            }

            var token = _tokenService.GenerateToken(login.Username);
            return Ok(new { Token = token });
        }
    }
}
