namespace CareerMatch.API.Models
{
    public class GeneratedCV
    {
        public int GeneratedCVId { get; set; }

        // The application for which this refined CV was created.
        public int ApplicationId { get; set; }

        // The job-specific CV text returned by OpenAI.
        public string GeneratedCVText { get; set; } = string.Empty;

        // Name of the PDF file after the user requests a download.
        public string? GeneratedPdfFileName { get; set; }

        // Server location of the generated PDF.
        public string? GeneratedPdfFilePath { get; set; }

        // Date and time when the CV was generated.
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        public JobApplication? JobApplication { get; set; }
    }
}