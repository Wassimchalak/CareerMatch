namespace CareerMatch.API.DTOs
{
    public class JobApplicationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? JobUrl { get; set; }
        public int ApplicationId { get; set; }
        public bool HasCV { get; set; }
    }
}