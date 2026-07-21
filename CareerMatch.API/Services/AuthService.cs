using CareerMatch.API.Data;
using CareerMatch.API.DTOs;
using CareerMatch.API.Models;
using Dapper;
using System.Security.Cryptography;
using System.Text;

namespace CareerMatch.API.Services
{
    // Handles registration, login, and password-reset workflows.
    public class AuthService
    {
        // Creates SQL Server connections for Dapper.
        private readonly DbConnectionFactory _dbConnectionFactory;

        // Creates signed JWT access tokens.
        private readonly JwtService _jwtService;

        // Sends password-reset emails.
        private readonly EmailService _emailService;

        // Reads reset-link settings.
        private readonly IConfiguration _configuration;

        // Receives all dependencies through ASP.NET Core dependency injection.
        public AuthService(
            DbConnectionFactory dbConnectionFactory,
            JwtService jwtService,
            EmailService emailService,
            IConfiguration configuration)
        {
            // Saves the Dapper connection factory.
            _dbConnectionFactory =
                dbConnectionFactory;

            // Saves the JWT service.
            _jwtService =
                jwtService;

            // Saves the email service.
            _emailService =
                emailService;

            // Saves configuration.
            _configuration =
                configuration;
        }

        // Registers a new user and immediately returns a JWT.
        public async Task<UserResponse?>
            RegisterAsync(
                RegisterRequest request)
        {
            // Opens a database connection.
            using var connection =
                _dbConnectionFactory
                    .CreateConnection();

            // Normalizes email to prevent duplicate casing variations.
            string normalizedEmail =
                NormalizeEmail(
                    request.Email
                );

            // Checks whether the email is already registered.
            int emailExists =
                await connection
                    .ExecuteScalarAsync<int>(
                        @"
                        SELECT COUNT(1)
                        FROM Users
                        WHERE LOWER(Email) =
                            @Email;
                        ",
                        new
                        {
                            // Uses the normalized value.
                            Email =
                                normalizedEmail
                        }
                    );

            // Returns null so the controller can return HTTP 400.
            if (emailExists > 0)
                return null;

            // Builds the user that will be inserted.
            var user =
                new User
                {
                    // Trims unnecessary spaces from the full name.
                    FullName =
                        request.FullName.Trim(),

                    // Stores the normalized email.
                    Email =
                        normalizedEmail,

                    // Stores a BCrypt password hash, never the plain password.
                    PasswordHash =
                        BCrypt.Net.BCrypt
                            .HashPassword(
                                request.Password
                            ),

                    // Uses UTC for consistent server timestamps.
                    CreatedAt =
                        DateTime.UtcNow
                };

            // Inserts the user and returns the generated primary key.
            user.UserId =
                await connection
                    .ExecuteScalarAsync<int>(
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

            // Creates the first access token.
            var tokenResult =
                _jwtService.GenerateToken(
                    user
                );

            // Returns the authenticated user response.
            return BuildUserResponse(
                user,
                tokenResult.Token,
                tokenResult.ExpiresAt
            );
        }

        // Verifies credentials and returns a JWT.
        public async Task<UserResponse?>
            LoginAsync(
                LoginRequest request)
        {
            // Opens a database connection.
            using var connection =
                _dbConnectionFactory
                    .CreateConnection();

            // Normalizes email before querying.
            string normalizedEmail =
                NormalizeEmail(
                    request.Email
                );

            // Loads the matching user.
            User? user =
                await connection
                    .QueryFirstOrDefaultAsync<User>(
                        @"
                        SELECT
                            UserId,
                            FullName,
                            Email,
                            PasswordHash,
                            CreatedAt
                        FROM Users
                        WHERE LOWER(Email) =
                            @Email;
                        ",
                        new
                        {
                            // Uses the normalized email.
                            Email =
                                normalizedEmail
                        }
                    );

            // Rejects unknown accounts.
            if (user == null)
                return null;

            // Verifies the supplied password against the BCrypt hash.
            bool isPasswordValid =
                BCrypt.Net.BCrypt.Verify(
                    request.Password,
                    user.PasswordHash
                );

            // Rejects an incorrect password.
            if (!isPasswordValid)
                return null;

            // Creates a new access token.
            var tokenResult =
                _jwtService.GenerateToken(
                    user
                );

            // Returns the authenticated user response.
            return BuildUserResponse(
                user,
                tokenResult.Token,
                tokenResult.ExpiresAt
            );
        }

        // Creates a one-time reset token and sends the reset link when the user exists.
        public async Task
            ForgotPasswordAsync(
                ForgotPasswordRequest request)
        {
            // Opens a database connection.
            using var connection =
                _dbConnectionFactory
                    .CreateConnection();

            // Normalizes email before querying.
            string normalizedEmail =
                NormalizeEmail(
                    request.Email
                );

            // Loads only the user data required for the email.
            User? user =
                await connection
                    .QueryFirstOrDefaultAsync<User>(
                        @"
                        SELECT
                            UserId,
                            FullName,
                            Email
                        FROM Users
                        WHERE LOWER(Email) =
                            @Email;
                        ",
                        new
                        {
                            // Uses the normalized email.
                            Email =
                                normalizedEmail
                        }
                    );

            // Silently returns when no account exists.
            // This prevents attackers from discovering registered emails.
            if (user == null)
                return;

            // Generates 32 cryptographically secure random bytes.
            byte[] tokenBytes =
                RandomNumberGenerator
                    .GetBytes(32);

            // Converts the bytes into a URL-safe token.
            string rawToken =
                Convert.ToBase64String(
                    tokenBytes
                )
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');

            // Hashes the raw token before database storage.
            string tokenHash =
                HashToken(rawToken);

            // Uses a short one-time expiration window.
            DateTime expiresAt =
                DateTime.UtcNow
                    .AddMinutes(30);

            // Uses one creation timestamp.
            DateTime createdAt =
                DateTime.UtcNow;

            // Invalidates previous unused tokens for this user.
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
                    // Identifies the user.
                    UserId =
                        user.UserId,

                    // Records when previous tokens were invalidated.
                    UsedAt =
                        createdAt
                }
            );

            // Inserts the new hashed token.
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
                    // Links the token to the user.
                    UserId =
                        user.UserId,

                    // Stores only the hash.
                    TokenHash =
                        tokenHash,

                    // Stores the expiry time.
                    ExpiresAt =
                        expiresAt,

                    // Stores creation time.
                    CreatedAt =
                        createdAt
                }
            );

            // Reads the React reset-password page URL.
            string frontendResetUrl =
                _configuration[
                    "Frontend:ResetPasswordUrl"
                ]
                ?? throw new Exception(
                    "Frontend:ResetPasswordUrl is missing."
                );

            // Appends the raw token only to the emailed link.
            string resetLink =
                $"{frontendResetUrl}?token={Uri.EscapeDataString(rawToken)}";

            // Sends the email after the token has been stored.
            await _emailService
                .SendPasswordResetEmailAsync(
                    user.Email,
                    user.FullName,
                    resetLink
                );
        }

        // Validates the reset token and changes the password once.
        public async Task<bool>
            ResetPasswordAsync(
                ResetPasswordRequest request)
        {
            // Hashes the token received from the frontend.
            string tokenHash =
                HashToken(
                    request.Token
                );

            // Opens a database connection.
            using var connection =
                _dbConnectionFactory
                    .CreateConnection();

            // Starts a transaction so password update and token consumption succeed together.
            connection.Open();

            // Creates the transaction.
            using var transaction =
                connection.BeginTransaction();

            try
            {
                // Loads one valid unused token.
                ResetTokenUserData? tokenData =
                    await connection
                        .QueryFirstOrDefaultAsync<
                            ResetTokenUserData>(
                            @"
                            SELECT TOP 1
                                prt.PasswordResetTokenId,
                                prt.UserId
                            FROM PasswordResetTokens prt
                            WHERE prt.TokenHash =
                                @TokenHash
                              AND prt.IsUsed = 0
                              AND prt.ExpiresAt >
                                @CurrentTime
                            ORDER BY prt.CreatedAt DESC;
                            ",
                            new
                            {
                                // Matches the hashed submitted token.
                                TokenHash =
                                    tokenHash,

                                // Rejects expired tokens.
                                CurrentTime =
                                    DateTime.UtcNow
                            },
                            transaction
                        );

                // Rolls back when the token is invalid, expired, or already used.
                if (tokenData == null)
                {
                    transaction.Rollback();
                    return false;
                }

                // Creates a BCrypt hash for the new password.
                string newPasswordHash =
                    BCrypt.Net.BCrypt
                        .HashPassword(
                            request.NewPassword
                        );

                // Updates the user's password.
                int updatedUsers =
                    await connection.ExecuteAsync(
                        @"
                        UPDATE Users
                        SET PasswordHash =
                            @PasswordHash
                        WHERE UserId =
                            @UserId;
                        ",
                        new
                        {
                            // Stores the new BCrypt hash.
                            PasswordHash =
                                newPasswordHash,

                            // Selects the token owner.
                            UserId =
                                tokenData.UserId
                        },
                        transaction
                    );

                // Marks the one-time token as consumed.
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
                            // Selects the reset token.
                            PasswordResetTokenId =
                                tokenData
                                    .PasswordResetTokenId,

                            // Records consumption time.
                            UsedAt =
                                DateTime.UtcNow
                        },
                        transaction
                    );

                // Rejects unexpected update failures.
                if (updatedUsers != 1 ||
                    updatedTokens != 1)
                {
                    transaction.Rollback();
                    return false;
                }

                // Makes both changes permanent.
                transaction.Commit();

                // Reports a successful password reset.
                return true;
            }
            catch
            {
                // Reverts all changes if any database operation fails.
                transaction.Rollback();

                // Preserves the original exception.
                throw;
            }
        }

        // Creates the common authenticated response.
        private static UserResponse
            BuildUserResponse(
                User user,
                string token,
                DateTime expiresAt)
        {
            // Maps the user and token into the DTO.
            return new UserResponse
            {
                // Returns the user id.
                UserId =
                    user.UserId,

                // Returns the user's name.
                FullName =
                    user.FullName,

                // Returns the normalized email.
                Email =
                    user.Email,

                // Returns the signed access token.
                Token =
                    token,

                // Returns token expiration.
                ExpiresAt =
                    expiresAt
            };
        }

        // Normalizes emails for consistent registration and login.
        private static string NormalizeEmail(
            string email)
        {
            // Removes surrounding spaces and normalizes casing.
            return email
                .Trim()
                .ToLowerInvariant();
        }

        // Creates the SHA-256 value stored for a reset token.
        private static string HashToken(
            string rawToken)
        {
            // Converts token text into bytes.
            byte[] tokenBytes =
                Encoding.UTF8.GetBytes(
                    rawToken
                );

            // Computes SHA-256.
            byte[] hashBytes =
                SHA256.HashData(
                    tokenBytes
                );

            // Stores the result as lowercase hexadecimal text.
            return Convert.ToHexString(
                    hashBytes
                )
                .ToLowerInvariant();
        }

        // Holds the token and user ids returned by the reset-token query.
        private class ResetTokenUserData
        {
            // Stores the token row id.
            public int PasswordResetTokenId
            {
                get;
                set;
            }

            // Stores the token owner.
            public int UserId
            {
                get;
                set;
            }
        }
    }
}
