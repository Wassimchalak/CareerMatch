namespace CareerMatch.API.DTOs
{
    public class InterviewQuestionItem
    {
        public int QuestionNumber { get; set; }
        public string Question { get; set; } = string.Empty;
        public string HowToAnswer { get; set; } = string.Empty;
    }
}
