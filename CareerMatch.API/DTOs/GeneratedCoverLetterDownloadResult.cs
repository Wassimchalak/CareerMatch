namespace CareerMatch.API.DTOs
{
    // Carries any generated PDF from a service to its controller.
    public class GeneratedDocumentDownloadResult
    {
        // Contains the complete PDF file.
        public byte[] FileBytes { get; set; } = Array.Empty<byte>();

        // Contains the filename shown to the applicant.
        public string FileName { get; set; } = string.Empty;

        // Tells the browser that this response is a PDF.
        public string ContentType { get; set; } = "application/pdf";
    }
}
