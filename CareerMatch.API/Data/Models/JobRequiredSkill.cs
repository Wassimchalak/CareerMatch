using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CareerMatch.API.Models
{
    public class JobRequiredSkill
    {
        public int JobRequiredSkillId { get; set; }
        public int JobId { get; set; }

        public int SkillId { get; set; }

        public decimal? RequiredYears { get; set; }

        public string Importance { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Job? Job { get; set; }

           public Skill? Skill { get; set; }
    }
}