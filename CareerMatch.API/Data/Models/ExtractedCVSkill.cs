using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CareerMatch.API.Models
{
    public class ExtractedCVSkill
    {
      
        public int ExtractedCVSkillId { get; set; }

      
        public int CVId { get; set; }

        public int SkillId { get; set; }

        public decimal? YearsOfExperience { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}