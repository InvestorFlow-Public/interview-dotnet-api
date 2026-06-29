using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace interview_dotnet_api
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost("token")]
        public IActionResult GenerateToken([FromBody] TokenRequest request)
        {
            var key = _configuration["Jwt:Key"] ?? "SuperSecretSecurityKeyThatIsLongEnoughToMeetRequirements32Chars!";
            var issuer = _configuration["Jwt:Issuer"] ?? "NotificationIssuer";
            var audience = _configuration["Jwt:Audience"] ?? "NotificationAudience";
            var expiresAt = DateTime.UtcNow.AddHours(1);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, request.UserId),
                new Claim(JwtRegisteredClaimNames.Sub, request.UserId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                expiresAt
            });
        }
    }

    public class TokenRequest
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
    }
}
