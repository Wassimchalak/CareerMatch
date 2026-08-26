namespace CareerMatch.API.DTOs
{
    public class AIInterviewQuestionsResult
    {
        public List<InterviewQuestionItem> TheoreticalQuestions { get; set; }
            = new List<InterviewQuestionItem>();

        public List<InterviewQuestionItem> PracticalQuestions { get; set; }
            = new List<InterviewQuestionItem>();
    }
}
