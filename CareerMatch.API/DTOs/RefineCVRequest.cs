namespace CareerMatch.API.DTOs
{
    // Receives the application whose CV should be improved and downloaded.
    public class GenerateCVRequest
    {
        // Identifies the exact JobApplications row.
        public int ApplicationId { get; set; }
    }
}
