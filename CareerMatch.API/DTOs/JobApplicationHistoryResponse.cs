namespace CareerMatch.API.DTOs
{
    public class JobApplicationHistoryResponse
    {
        public int ApplicationId { get; set; }

        public int JobId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public string? City { get; set; }

        public string JobUrl { get; set; } = string.Empty;

        public string ApplicationStatus { get; set; } = string.Empty;

        public DateTime AppliedAt { get; set; }

        public decimal? MatchScore { get; set; }

        public string? MatchExplanation { get; set; }

        public string? Recommendation { get; set; }
    }
}