namespace CareerMatch.API.Models
{
    public class SavedJob
    {
        public int SavedJobId { get; set; }

        public int UserId { get; set; }

        public int JobId { get; set; }

        public decimal? MatchScoreAtSave { get; set; }

        public string? SavedMatchExplanation { get; set; }

        public DateTime SavedAt { get; set; } = DateTime.Now;

        public User? User { get; set; }

        public Job? Job { get; set; }
    }
}