using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CareerMatch.API.Models
{
    public class CV
    {
     
        public int CVId { get; set; }

        public int UserId { get; set; }

        public string OriginalFileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;
         public string? CVTextHash{ get; set; } = string.Empty;

        public string ExtractedText { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; } = DateTime.Now;
        public string? PrimaryRole { get; set; }

      
    }
}