namespace CareerMatch.API.DTOs
{

    public class GeneratedCVDownloadResult
    {
        public byte[] FileBytes { get; set; } = Array.Empty<byte>();

        public string FileName { get; set; } = string.Empty;

        public string ContentType { get; set; } = "application/pdf";
    }
}