namespace CareerMatch.API.DTOs
{
    // Represents the body received by the generate endpoint.
    public class GenerateInterviewQuestionsRequest
    {
        // Identifies the exact job application used to load the CV and job.
        public int ApplicationId { get; set; }
    }
}
