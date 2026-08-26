using CareerMatch.API.Data;
using CareerMatch.API.DTOs;
using CareerMatch.API.Models;
using Dapper;
using System.Security.Cryptography;
using System.Text;

namespace CareerMatch.API.Services
{
    public class AuthService
    {
        private readonly DbConnectionFactory _dbConnectionFactory;
        private readonly JwtService _jwtService;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;

        public AuthService(
            DbConnectionFactory dbConnectionFactory,
            JwtService jwtService,
            EmailService emailService,
            IConfiguration configuration)
        {
            _dbConnectionFactory = dbConnectionFactory;
            _jwtService = jwtService;
            _emailService = emailService;
            _configuration = configuration;
        }

        public async Task<UserResponse?> RegisterAsync(
            RegisterRequest request)
        {
            using var connection =
                _dbConnectionFactory.CreateConnection();

            string normalizedEmail =
                NormalizeEmail(request.Email);

            int emailExists =
                await connection.ExecuteScalarAsync<int>(
                    @"
                    SELECT COUNT(1)
                    FROM Users
                    WHERE LOWER(Email) = @Email;
                    ",
                    new
                    {
                        Email = normalizedEmail
                    }
                );

            if (emailExists > 0)
            {
                return null;
            }

            var user = new User
            {
                FullName = request.FullName.Trim(),
                Email = normalizedEmail,
                PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(
                        request.Password
                    ),
                CreatedAt = DateTime.UtcNow
            };

            user.UserId =
                await connection.ExecuteScalarAsync<int>(
                    @"
                    INSERT INTO Users
                    (
                        FullName,
                        Email,
                        PasswordHash,
                        CreatedAt
                    )
                    OUTPUT INSERTED.UserId
                    VALUES
                    (
                        @FullName,
                        @Email,
                        @PasswordHash,
                        @CreatedAt
                    );
                    ",
                    user
                );

            var tokenResult =
                _jwtService.GenerateToken(user);

            return BuildUserResponse(
                user,
                tokenResult.Token,
                tokenResult.ExpiresAt
            );
        }

        public async Task<UserResponse?> LoginAsync(
            LoginRequest request)
        {
            using var connection =
                _dbConnectionFactory.CreateConnection();

            string normalizedEmail =
                NormalizeEmail(request.Email);

            User? user =
                await connection.QueryFirstOrDefaultAsync<User>(
                    @"
                    SELECT
                        UserId,
                        FullName,
                        Email,
                        PasswordHash,
                        CreatedAt
                    FROM Users
                    WHERE LOWER(Email) = @Email;
                    ",
                    new
                    {
                        Email = normalizedEmail
                    }
                );

            if (user == null)
            {
                return null;
            }

            bool isPasswordValid =
                BCrypt.Net.BCrypt.Verify(
                    request.Password,
                    user.PasswordHash
                );

            if (!isPasswordValid)
            {
                return null;
            }

            var tokenResult =
                _jwtService.GenerateToken(user);

            return BuildUserResponse(
                user,
                tokenResult.Token,
                tokenResult.ExpiresAt
            );
        }

        public async Task ForgotPasswordAsync(
            ForgotPasswordRequest request)
        {
            using var connection =
                _dbConnectionFactory.CreateConnection();

            string normalizedEmail =
                NormalizeEmail(request.Email);

            User? user =
                await connection.QueryFirstOrDefaultAsync<User>(
                    @"
                    SELECT
                        UserId,
                        FullName,
                        Email
                    FROM Users
                    WHERE LOWER(Email) = @Email;
                    ",
                    new
                    {
                        Email = normalizedEmail
                    }
                );

            if (user == null)
            {
                return;
            }

            byte[] tokenBytes =
                RandomNumberGenerator.GetBytes(32);

            string rawToken =
                Convert.ToBase64String(tokenBytes)
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .TrimEnd('=');

            string tokenHash =
                HashToken(rawToken);

            DateTime expiresAt =
                DateTime.UtcNow.AddMinutes(30);

            DateTime createdAt =
                DateTime.UtcNow;

            await connection.ExecuteAsync(
                @"
                UPDATE PasswordResetTokens
                SET
                    IsUsed = 1,
                    UsedAt = @UsedAt
                WHERE UserId = @UserId
                  AND IsUsed = 0;
                ",
                new
                {
                    UserId = user.UserId,
                    UsedAt = createdAt
                }
            );

            await connection.ExecuteAsync(
                @"
                INSERT INTO PasswordResetTokens
                (
                    UserId,
                    TokenHash,
                    ExpiresAt,
                    IsUsed,
                    CreatedAt,
                    UsedAt
                )
                VALUES
                (
                    @UserId,
                    @TokenHash,
                    @ExpiresAt,
                    0,
                    @CreatedAt,
                    NULL
                );
                ",
                new
                {
                    UserId = user.UserId,
                    TokenHash = tokenHash,
                    ExpiresAt = expiresAt,
                    CreatedAt = createdAt
                }
            );

            string frontendResetUrl =
                _configuration[
                    "Frontend:ResetPasswordUrl"
                ]
                ?? throw new Exception(
                    "Frontend:ResetPasswordUrl is missing."
                );

            string resetLink =
                $"{frontendResetUrl}?token={Uri.EscapeDataString(rawToken)}";

            await _emailService.SendPasswordResetEmailAsync(
                user.Email,
                user.FullName,
                resetLink
            );
        }

        public async Task<bool> ResetPasswordAsync(
            ResetPasswordRequest request)
        {
            string tokenHash =
                HashToken(request.Token);

            using var connection =
                _dbConnectionFactory.CreateConnection();

            connection.Open();

            using var transaction =
                connection.BeginTransaction();

            try
            {
                ResetTokenUserData? tokenData =
                    await connection
                        .QueryFirstOrDefaultAsync<ResetTokenUserData>(
                            @"
                            SELECT TOP 1
                                prt.PasswordResetTokenId,
                                prt.UserId
                            FROM PasswordResetTokens prt
                            WHERE prt.TokenHash = @TokenHash
                              AND prt.IsUsed = 0
                              AND prt.ExpiresAt > @CurrentTime
                            ORDER BY prt.CreatedAt DESC;
                            ",
                            new
                            {
                                TokenHash = tokenHash,
                                CurrentTime = DateTime.UtcNow
                            },
                            transaction
                        );

                if (tokenData == null)
                {
                    transaction.Rollback();
                    return false;
                }

                string newPasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(
                        request.NewPassword
                    );

                int updatedUsers =
                    await connection.ExecuteAsync(
                        @"
                        UPDATE Users
                        SET PasswordHash = @PasswordHash
                        WHERE UserId = @UserId;
                        ",
                        new
                        {
                            PasswordHash = newPasswordHash,
                            UserId = tokenData.UserId
                        },
                        transaction
                    );

                int updatedTokens =
                    await connection.ExecuteAsync(
                        @"
                        UPDATE PasswordResetTokens
                        SET
                            IsUsed = 1,
                            UsedAt = @UsedAt
                        WHERE PasswordResetTokenId =
                            @PasswordResetTokenId
                          AND IsUsed = 0;
                        ",
                        new
                        {
                            PasswordResetTokenId =
                                tokenData.PasswordResetTokenId,
                            UsedAt = DateTime.UtcNow
                        },
                        transaction
                    );

                if (updatedUsers != 1 ||
                    updatedTokens != 1)
                {
                    transaction.Rollback();
                    return false;
                }

                transaction.Commit();

                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static UserResponse BuildUserResponse(
            User user,
            string token,
            DateTime expiresAt)
        {
            return new UserResponse
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Token = token,
                ExpiresAt = expiresAt
            };
        }

        private static string NormalizeEmail(
            string email)
        {
            return email
                .Trim()
                .ToLowerInvariant();
        }

        private static string HashToken(
            string rawToken)
        {
            byte[] tokenBytes =
                Encoding.UTF8.GetBytes(rawToken);

            byte[] hashBytes =
                SHA256.HashData(tokenBytes);

            return Convert
                .ToHexString(hashBytes)
                .ToLowerInvariant();
        }

        private class ResetTokenUserData
        {
            public int PasswordResetTokenId
            {
                get;
                set;
            }

            public int UserId
            {
                get;
                set;
            }
        }
    }
}