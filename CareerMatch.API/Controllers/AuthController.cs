using CareerMatch.API.DTOs;
using CareerMatch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareerMatch.API.Controllers
{
    // Enables automatic API validation and request binding.
    [ApiController]

    // Creates the base route /api/Auth.
    [Route("api/[controller]")]
    public class AuthController
        : ControllerBase
    {
        // Contains registration, login, and password-reset logic.
        private readonly AuthService
            _authService;

        // Receives AuthService through dependency injection.
        public AuthController(
            AuthService authService)
        {
            // Saves the injected service.
            _authService =
                authService;
        }

        // Allows unauthenticated users to create an account.
        [AllowAnonymous]

        // Handles POST /api/Auth/register.
        [HttpPost("register")]
        public async Task<IActionResult>
            Register(
                RegisterRequest request)
        {
            // Registers the user and creates a JWT.
            UserResponse? result =
                await _authService
                    .RegisterAsync(request);

            // Rejects duplicate email addresses.
            if (result == null)
            {
                return BadRequest(
                    "Email already exists."
                );
            }

            // Returns the user and JWT.
            return Ok(result);
        }

        // Allows unauthenticated users to log in.
        [AllowAnonymous]

        // Handles POST /api/Auth/login.
        [HttpPost("login")]
        public async Task<IActionResult>
            Login(
                LoginRequest request)
        {
            // Verifies credentials and creates a JWT.
            UserResponse? result =
                await _authService
                    .LoginAsync(request);

            // Rejects invalid credentials.
            if (result == null)
            {
                return Unauthorized(
                    "Invalid email or password."
                );
            }

            // Returns the user and JWT.
            return Ok(result);
        }

        // Allows users who cannot log in to request a reset link.
        [AllowAnonymous]

        // Handles POST /api/Auth/forgot-password.
        [HttpPost("forgot-password")]
        public async Task<IActionResult>
            ForgotPassword(
                ForgotPasswordRequest request)
        {
            // Creates and emails a reset token when the account exists.
            await _authService
                .ForgotPasswordAsync(request);

            // Always returns the same response to prevent account enumeration.
            return Ok(
                "If an account exists for this email, a password reset link has been sent."
            );
        }

        // Allows a user with a valid reset token to choose a new password.
        [AllowAnonymous]

        // Handles POST /api/Auth/reset-password.
        [HttpPost("reset-password")]
        public async Task<IActionResult>
            ResetPassword(
                ResetPasswordRequest request)
        {
            // Validates the token and changes the password.
            bool resetSucceeded =
                await _authService
                    .ResetPasswordAsync(request);

            // Rejects invalid, expired, or previously used tokens.
            if (!resetSucceeded)
            {
                return BadRequest(
                    "The password reset link is invalid or has expired."
                );
            }

            // Confirms that the password was changed.
            return Ok(
                "Password reset successfully."
            );
        }
    }
}
