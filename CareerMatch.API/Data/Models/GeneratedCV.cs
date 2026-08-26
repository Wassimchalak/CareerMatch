namespace CareerMatch.API.Models
{
    public class GeneratedCV
    {
        public int GeneratedCVId { get; set; }

        public int ApplicationId { get; set; }

        public string GeneratedCVText { get; set; } = string.Empty;

        public string? GeneratedPdfFileName { get; set; }

        public string? GeneratedPdfFilePath { get; set; }

        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        public JobApplication? JobApplication { get; set; }
    }
}