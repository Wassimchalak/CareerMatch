namespace CareerMatch.API.DTOs
{
    public class SavedJobScoreResponse
    {
        public int JobId { get; set; }

        public decimal MatchScore { get; set; }

        public string? MatchExplanation { get; set; }

        public string? Recommendation { get; set; }
    }
}