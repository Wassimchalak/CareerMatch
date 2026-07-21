namespace CareerMatch.API.DTOs
{
    public class AIMatchResult
    {
        public int JobId { get; set; }

        public decimal MatchScore { get; set; }

        public string MatchExplanation { get; set; }
            = string.Empty;

        public string Recommendation { get; set; }
            = string.Empty;

        public List<string> MatchedSkills { get; set; }
            = new();

        public List<string> MissingSkills { get; set; }
            = new();
    }
}