using System.ComponentModel.DataAnnotations;

namespace CareerMatch.API.Models
{
    public class Skill
    {
      
        public int SkillId { get; set; }

      
        public string SkillName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}