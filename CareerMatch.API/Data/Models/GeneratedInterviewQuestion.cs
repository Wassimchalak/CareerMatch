namespace CareerMatch.API.Models
{
    // Represents one saved set of interview questions for one job application.
    public class GeneratedInterviewQuestion
    {
        // Stores the primary key from GeneratedInterviewQuestions.
        public int GeneratedInterviewQuestionId { get; set; }

        // Stores the JobApplications.ApplicationId that these questions belong to.
        public int ApplicationId { get; set; }

        // Stores the complete AI result as JSON, including questions and suggested answers.
        public string GeneratedQuestions { get; set; } = string.Empty;

        // Stores when this interview-preparation set was generated.
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        // Provides an optional in-memory navigation property.
        // Dapper does not populate this automatically unless you explicitly map it.
        public JobApplication? JobApplication { get; set; }
    }
}
