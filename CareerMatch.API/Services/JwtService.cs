using CareerMatch.API.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CareerMatch.API.Services
{
    // Creates signed JWT access tokens for authenticated CareerMatch users.
    public class JwtService
    {
        // Reads JWT settings from appsettings.json, user secrets, or environment variables.
        private readonly IConfiguration _configuration;

        // Receives configuration through dependency injection.
        public JwtService(IConfiguration configuration)
        {
            // Saves configuration for token generation.
            _configuration = configuration;
        }

        // Creates a signed access token and returns its expiration time.
        public (string Token, DateTime ExpiresAt)
            GenerateToken(User user)
        {
            // Reads the signing key.
            string key =
                _configuration["Jwt:Key"]
                ?? throw new Exception(
                    "Jwt:Key is missing."
                );

            // Requires a strong key suitable for HMAC SHA-256.
            if (key.Length < 32)
            {
                throw new Exception(
                    "Jwt:Key must contain at least 32 characters."
                );
            }

            // Reads the expected token issuer.
            string issuer =
                _configuration["Jwt:Issuer"]
                ?? "CareerMatch";

            // Reads the expected token audience.
            string audience =
                _configuration["Jwt:Audience"]
                ?? "CareerMatchUsers";

            // Reads token lifetime and falls back to two hours.
            int expireMinutes =
                int.TryParse(
                    _configuration[
                        "Jwt:ExpireMinutes"
                    ],
                    out int configuredMinutes
                )
                    ? configuredMinutes
                    : 120;

            // Uses UTC so token validation is timezone independent.
            DateTime expiresAt =
                DateTime.UtcNow.AddMinutes(
                    expireMinutes
                );

            // Stores the user id in NameIdentifier for secure ownership checks.
            var claims =
                new List<Claim>
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        user.UserId.ToString()
                    ),

                    // Stores the email address in the token.
                    new Claim(
                        ClaimTypes.Email,
                        user.Email
                    ),

                    // Stores the full name for frontend display when useful.
                    new Claim(
                        ClaimTypes.Name,
                        user.FullName
                    ),

                    // Adds a unique id to every access token.
                    new Claim(
                        JwtRegisteredClaimNames.Jti,
                        Guid.NewGuid().ToString()
                    )
                };

            // Converts the secret text into signing bytes.
            var securityKey =
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(key)
                );

            // Selects HMAC SHA-256 signing.
            var credentials =
                new SigningCredentials(
                    securityKey,
                    SecurityAlgorithms.HmacSha256
                );

            // Creates the JWT object.
            var token =
                new JwtSecurityToken(
                    issuer: issuer,
                    audience: audience,
                    claims: claims,
                    notBefore: DateTime.UtcNow,
                    expires: expiresAt,
                    signingCredentials: credentials
                );

            // Serializes the JWT into the compact token string.
            string tokenValue =
                new JwtSecurityTokenHandler()
                    .WriteToken(token);

            // Returns both values needed by the login response.
            return (tokenValue, expiresAt);
        }
    }
}
