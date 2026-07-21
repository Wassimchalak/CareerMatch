namespace CareerMatch.API.DTOs
{
    // Represents one interview question and its preparation guidance.
    public class InterviewQuestionItem
    {
        // Stores the question number displayed in the API response and PDF.
        public int QuestionNumber { get; set; }

        // Stores the interview question itself.
        public string Question { get; set; } = string.Empty;

        // Stores a strong example answer or solution.
        public string SuggestedAnswer { get; set; } = string.Empty;

        // Stores practical guidance about what the applicant should mention.
        public string HowToAnswer { get; set; } = string.Empty;
    }
}
