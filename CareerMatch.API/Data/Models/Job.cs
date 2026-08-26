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

        public string DescriptionHash { get; set; } = string.Empty;

        public string? ClassificationHash { get; set; }

        public string JobUrl { get; set; } = string.Empty;

        public string? EmploymentType { get; set; }

        public string? WorkMode { get; set; }

        public DateTime? ClassifiedAt { get; set; }

        public DateTime? PostedDate { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? PrimaryRole { get; set; }
    }
}