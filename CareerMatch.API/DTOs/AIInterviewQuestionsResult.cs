namespace CareerMatch.API.DTOs
{
    // Represents the exact JSON object expected from OpenAI.
    public class AIInterviewQuestionsResult
    {
        // Stores exactly five knowledge-based interview questions.
        public List<InterviewQuestionItem> TheoreticalQuestions { get; set; }
            = new List<InterviewQuestionItem>();

        // Stores exactly five coding, technical, case-study, or scenario questions.
        public List<InterviewQuestionItem> PracticalQuestions { get; set; }
            = new List<InterviewQuestionItem>();
    }
}
