namespace CareerMatch.API.DTOs
{
    public class AIJobAnalysisResult
    {
        public string PrimaryRole { get; set; } = string.Empty;

        public List<AIRequiredSkill> Skills { get; set; } = new();
    }
}