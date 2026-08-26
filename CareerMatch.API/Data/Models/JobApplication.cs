namespace CareerMatch.API.Models
{
    public class JobApplication
    {
        public int ApplicationId { get; set; }
        public int UserId { get; set; }

        public int? CVId { get; set; }

        public int JobId { get; set; }
        public string ApplicationStatus { get; set; } = string.Empty;
        public DateTime AppliedAt { get; set; }
    }
}