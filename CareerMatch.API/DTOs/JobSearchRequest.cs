namespace CareerMatch.API.DTOs
{
    // Contains job-search filters only. The authenticated user comes from JWT.
    public class JobSearchRequest
    {
        public string Country { get; set; } = string.Empty;

        public string? City { get; set; }

        public string Role { get; set; } = string.Empty;

        public string WorkType { get; set; } = string.Empty;

        public string EmploymentType { get; set; } = string.Empty;
    }
}
