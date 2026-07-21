namespace CareerMatch.API.DTOs
{
    // This DTO returns the result of the apply action to the frontend.
    public class JobApplicationResponse
    {
        // Tells the frontend if the application was saved successfully.
        public bool Success { get; set; }

        // Message explaining what happened.
        public string Message { get; set; } = string.Empty;

        // The external job URL that the frontend should open.
        public string? JobUrl { get; set; }
        public int ApplicationId { get; set; }
    }
}