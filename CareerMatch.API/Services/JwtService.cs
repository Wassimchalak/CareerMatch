using CareerMatch.API.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CareerMatch.API.Services
{
    public class JwtService
    {
        private readonly IConfiguration _configuration;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public (string Token, DateTime ExpiresAt)
            GenerateToken(User user)
        {
            string key =
                _configuration["Jwt:Key"]
                ?? throw new Exception(
                    "Jwt:Key is missing."
                );

            if (key.Length < 32)
            {
                throw new Exception(
                    "Jwt:Key must contain at least 32 characters."
                );
            }

            string issuer =
                _configuration["Jwt:Issuer"]
                ?? "CareerMatch";

            string audience =
                _configuration["Jwt:Audience"]
                ?? "CareerMatchUsers";

            int expireMinutes =
                int.TryParse(
                    _configuration[
                        "Jwt:ExpireMinutes"
                    ],
                    out int configuredMinutes
                )
                    ? configuredMinutes
                    : 120;

            DateTime expiresAt =
                DateTime.UtcNow.AddMinutes(
                    expireMinutes
                );

            var claims =
                new List<Claim>
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        user.UserId.ToString()
                    ),

                    new Claim(
                        ClaimTypes.Email,
                        user.Email
                    ),

                    new Claim(
                        ClaimTypes.Name,
                        user.FullName
                    ),

                    new Claim(
                        JwtRegisteredClaimNames.Jti,
                        Guid.NewGuid().ToString()
                    )
                };

            var securityKey =
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(key)
                );

            var credentials =
                new SigningCredentials(
                    securityKey,
                    SecurityAlgorithms.HmacSha256
                );

            var token =
                new JwtSecurityToken(
                    issuer: issuer,
                    audience: audience,
                    claims: claims,
                    notBefore: DateTime.UtcNow,
                    expires: expiresAt,
                    signingCredentials: credentials
                );

            string tokenValue =
                new JwtSecurityTokenHandler()
                    .WriteToken(token);

            return (tokenValue, expiresAt);
        }
    }
}