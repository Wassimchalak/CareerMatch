namespace CareerMatch.API.DTOs
{
    public class AIRequiredSkill
    {
        public string SkillName { get; set; } = string.Empty;

        public decimal RequiredYears { get; set; }

        public string Importance { get; set; } = "Required";
    }
}