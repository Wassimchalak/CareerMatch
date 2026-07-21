namespace CareerMatch.API.DTOs
{
    public class CVResponse
    {
        public int CVId { get; set; }

        public int UserId { get; set; }

        public string OriginalFileName { get; set; } = string.Empty;

        public string StoredFileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; }
    }
}