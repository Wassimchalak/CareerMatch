namespace CareerMatch.API.Models
{
    // Represents one password-reset request stored in SQL Server.
    public class PasswordResetToken
    {
        // Stores the primary key of the reset-token row.
        public int PasswordResetTokenId { get; set; }

        // Stores the user who requested the password reset.
        public int UserId { get; set; }

        // Stores only the SHA-256 hash of the token, never the raw token.
        public string TokenHash { get; set; } = string.Empty;

        // Stores the time after which the token can no longer be used.
        public DateTime ExpiresAt { get; set; }

        // Records whether the one-time token has already been consumed.
        public bool IsUsed { get; set; }

        // Stores when the reset request was created.
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Stores when the token was used, when applicable.
        public DateTime? UsedAt { get; set; }
    }
}
