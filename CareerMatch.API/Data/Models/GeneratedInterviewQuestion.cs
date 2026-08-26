namespace CareerMatch.API.Models
{
    public class GeneratedInterviewQuestion
    {
        public int GeneratedInterviewQuestionId { get; set; }

        public int ApplicationId { get; set; }

        public string GeneratedQuestions { get; set; } = string.Empty;

        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        public JobApplication? JobApplication { get; set; }
    }
}