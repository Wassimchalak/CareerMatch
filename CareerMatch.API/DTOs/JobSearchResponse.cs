namespace CareerMatch.API.DTOs
{
    public class JobSearchResponse
    {
        public int JobId { get; set; }

        public string ExternalJobId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public string? City { get; set; }

        public string Description { get; set; } = string.Empty;

        public string JobUrl { get; set; } = string.Empty;

        public string? EmploymentType { get; set; }

        public string? WorkMode { get; set; }

        public DateTime? PostedDate { get; set; }

        // Null while the score is still being calculated.
        public decimal? MatchScore { get; set; }

        public string? MatchExplanation { get; set; }

        public string? Recommendation { get; set; }

        // Pending, Completed, or Failed.
        public string MatchStatus { get; set; } = "Pending";
    }
}