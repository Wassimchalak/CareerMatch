namespace CareerMatch.API.Models
{
    public class JobApplication
    {
        public int ApplicationId { get; set; }
        public int UserId { get; set; }

        // Nullable because users may apply externally without first
        // uploading a CV to CareerMatch.
        public int? CVId { get; set; }

        public int JobId { get; set; }
        public string ApplicationStatus { get; set; } = string.Empty;
        public DateTime AppliedAt { get; set; }
    }
}