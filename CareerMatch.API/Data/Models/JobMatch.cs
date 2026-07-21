namespace CareerMatch.API.Models
{
    public class JobMatch
    {
        public int JobMatchId { get; set; }

        public int UserId { get; set; }

        public int CVId { get; set; }

        public int JobId { get; set; }

        public decimal FinalScore { get; set; }

        public string MatchExplanation { get; set; } = string.Empty;
        
        public string DescriptionHash { get; set; } = string.Empty;
          public string CVTextHash { get; set; } = string.Empty;

        public string Recommendation { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}