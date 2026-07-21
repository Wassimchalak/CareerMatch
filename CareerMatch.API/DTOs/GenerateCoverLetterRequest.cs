namespace CareerMatch.API.DTOs
{
    // Receives the application whose cover letter should be generated and downloaded.
    public class GenerateCoverLetterRequest
    {
        // Identifies the exact JobApplications row.
        public int ApplicationId { get; set; }
    }
}
