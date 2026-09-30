public class AICVAnalysisResult
{
    public string PrimaryRole { get; set; } = string.Empty;

    public string ExperienceLevel { get; set; } = "Unknown";

    public decimal ProfessionalYearsOfExperience { get; set; }

    public List<AISkillResult> Skills { get; set; } = new();
}