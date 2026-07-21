namespace CareerMatch.API.DTOs
{
    // Represents the authenticated user returned after registration or login.
    public class UserResponse
    {
        // Returns the authenticated user's database identifier.
        public int UserId { get; set; }

        // Returns the user's full name.
        public string FullName { get; set; } = string.Empty;

        // Returns the user's normalized email address.
        public string Email { get; set; } = string.Empty;

        // Returns the JWT access token used in protected API requests.
        public string Token { get; set; } = string.Empty;

        // Tells the frontend when the JWT access token expires.
        public DateTime ExpiresAt { get; set; }
    }
}
