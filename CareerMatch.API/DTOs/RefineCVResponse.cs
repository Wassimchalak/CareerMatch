namespace CareerMatch.API.DTOs
{
    public class RefineCVResponse
    {
        public int GeneratedCVId { get; set; }

        public int ApplicationId { get; set; }

        public string GeneratedCVText { get; set; } = string.Empty;

        public DateTime GeneratedAt { get; set; }
    }
}