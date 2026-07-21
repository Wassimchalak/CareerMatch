using System.ComponentModel.DataAnnotations;

namespace CareerMatch.API.DTOs
{
    // Represents the forgot-password request body.
    public class ForgotPasswordRequest
    {
        // Requires a correctly formatted email address.
        [Required]
        [EmailAddress]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;
    }
}
