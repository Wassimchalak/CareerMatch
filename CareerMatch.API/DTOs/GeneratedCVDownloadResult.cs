namespace CareerMatch.API.DTOs
{
    // Represents the PDF file returned by the service.
    public class GeneratedCVDownloadResult
    {
        // PDF content as bytes.
        public byte[] FileBytes { get; set; } = Array.Empty<byte>();

        // Name that the browser will use when downloading the file.
        public string FileName { get; set; } = string.Empty;

        // MIME type for PDF files.
        public string ContentType { get; set; } = "application/pdf";
    }
}