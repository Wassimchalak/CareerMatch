namespace CareerMatch.API.DTOs
{
    /// <summary>
    /// Represents one classified job returned by OpenAI.
    /// </summary>
    public class AIJobClassificationItem
    {
        // Local database JobId.
        public int JobId { get; set; }

        // One of:
        // Full-time
        // Part-time
        // Contract
        // Internship
        public string EmploymentType { get; set; }
            = string.Empty;

        // One of:
        // On-site
        // Remote
        // Hybrid
        public string WorkMode { get; set; }
            = string.Empty;
    }
}