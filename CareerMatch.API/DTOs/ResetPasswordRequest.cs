using System.ComponentModel.DataAnnotations;

namespace CareerMatch.API.DTOs
{
    // Represents the request used to choose a new password.
    public class ResetPasswordRequest
    {
        // Contains the one-time token received from the reset-password link.
        [Required]
        public string Token { get; set; } = string.Empty;

        // Contains the user's new password.
        [Required]
        [MinLength(8)]
        [MaxLength(100)]
        public string NewPassword { get; set; } = string.Empty;

        // Confirms that the user typed the intended password.
        [Required]
        [Compare(nameof(NewPassword))]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
