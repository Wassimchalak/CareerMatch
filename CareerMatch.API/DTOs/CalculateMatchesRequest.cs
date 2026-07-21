namespace CareerMatch.API.DTOs
{
    // Contains the jobs and preferences needed for matching.
    // UserId is intentionally read from the authenticated JWT.
    public class CalculateMatchesRequest
    {
        public List<int> JobIds { get; set; } = new();

        public string Country { get; set; } = string.Empty;

        public string? City { get; set; }

        public string Role { get; set; } = string.Empty;

        public string WorkType { get; set; } = string.Empty;

        public string EmploymentType { get; set; } = string.Empty;
    }
}
