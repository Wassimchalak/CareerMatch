namespace CareerMatch.API.DTOs
{
    // Receives only the PDF. UserId is taken from the authenticated JWT.
    public class CVUploadRequest
    {
        public IFormFile File { get; set; } = null!;
    }
}
