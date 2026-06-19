using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using interview_dotnet_api.Data;
using interview_dotnet_api.DTOs;
using interview_dotnet_api.Models;

namespace interview_dotnet_api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly NotificationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly PasswordHasher<User> _passwordHasher = new();

        public AuthController(NotificationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // POST: api/auth/register
        [HttpPost("register")]
        public async Task<ActionResult> Register(RegisterDto dto)
        {
            var usernameTaken = await _context.Users.AnyAsync(u => u.Username == dto.Username);
            if (usernameTaken)
            {
                return Conflict("Username is already taken.");
            }

            var user = new User { Username = dto.Username };
            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Created(string.Empty, new { user.Id, user.Username });
        }

        // POST: api/auth/login
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == dto.Username);
            if (user == null)
            {
                return Unauthorized("Invalid username or password.");
            }

            var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
            if (verification == PasswordVerificationResult.Failed)
            {
                return Unauthorized("Invalid username or password.");
            }

            var expiresAt = DateTime.UtcNow.AddHours(1);
            var token = GenerateToken(user, expiresAt);

            return Ok(new AuthResponseDto { Token = token, ExpiresAt = expiresAt });
        }

        private string GenerateToken(User user, DateTime expiresAt)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _configuration["Jwt:Key"] ?? "SuperSecretSecurityKeyThatIsLongEnoughToMeetRequirements32Chars!"));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"] ?? "NotificationIssuer",
                audience: _configuration["Jwt:Audience"] ?? "NotificationAudience",
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
