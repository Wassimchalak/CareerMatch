namespace CareerMatch.API.Models
{
    public class JobApplication
    {
        // Primary key of the application.
        public int ApplicationId { get; set; }

        // User who applied.
        public int UserId { get; set; }

        // Job that the user applied to.
        public int JobId { get; set; }

        // CV used for this application.
        public int CVId { get; set; }

        // Match result associated with this application.
        public int? JobMatchId { get; set; }

        // Current application status.
        // Examples: Applied, Under Review, Interview, Rejected, Accepted.
        public string ApplicationStatus { get; set; } = "Applied";

        // Date and time the application was submitted.
        public DateTime AppliedAt { get; set; } = DateTime.Now;
    }
}