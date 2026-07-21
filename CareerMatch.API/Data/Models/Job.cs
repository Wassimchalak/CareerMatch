namespace CareerMatch.API.Models
{
    public class Job
    {
        public int JobId { get; set; }

        public string ExternalJobId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public string? City { get; set; }

        public string Description { get; set; } = string.Empty;

        // Used for candidate matching cache.
        public string DescriptionHash { get; set; } = string.Empty;

        // Used for OpenAI classification cache.
        public string? ClassificationHash { get; set; }

        public string JobUrl { get; set; } = string.Empty;

        // Filled by OpenAI.
        public string? EmploymentType { get; set; }

        // Filled by OpenAI.
        public string? WorkMode { get; set; }

        // When OpenAI classified this job.
        public DateTime? ClassifiedAt { get; set; }

        public DateTime? PostedDate { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? PrimaryRole { get; set; }
    }
}