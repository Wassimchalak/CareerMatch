namespace CareerMatch.API.Models
{
    public class GeneratedCoverLetter
    {
        // Primary key of the generated cover letter.
        public int GeneratedCoverLetterId { get; set; }

        // The job application for which the cover letter was generated.
        public int ApplicationId { get; set; }

        // The cover letter text returned by OpenAI.
        public string CoverLetterText { get; set; } = string.Empty;

        // Name of the generated PDF file.
        public string? GeneratedPdfFileName { get; set; }

        // Full path of the generated PDF on the server.
        public string? GeneratedPdfFilePath { get; set; }

        // Date and time when the cover letter was generated.
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        // Optional navigation property.
        public JobApplication? JobApplication { get; set; }
    }
}