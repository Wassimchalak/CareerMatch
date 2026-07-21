namespace CareerMatch.API.DTOs
{
    public class AICVAnalysisResult
    {
        public string PrimaryRole { get; set; } = string.Empty;

        public List<AIExtractedSkill> Skills { get; set; } = new();
    }
}