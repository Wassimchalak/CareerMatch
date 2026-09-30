namespace CareerMatch.API.DTOs
{
    public class AICVAnalysisResult
    {
        public string PrimaryRole { get; set; } = string.Empty;

        public string ExperienceLevel { get; set; } = "Unknown";

        public decimal ProfessionalYearsOfExperience { get; set; }

        public List<AIExtractedSkill> Skills { get; set; } = new();
    }
}