namespace CareerMatch.API.DTOs
{
    public class SavedJobResponse
    {
        public int SavedJobId { get; set; }

        public int JobId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public string? City { get; set; }

        public string JobUrl { get; set; } = string.Empty;

        public decimal? MatchScoreAtSave { get; set; }

        public string? SavedMatchExplanation { get; set; }

        public DateTime SavedAt { get; set; }
    }
}