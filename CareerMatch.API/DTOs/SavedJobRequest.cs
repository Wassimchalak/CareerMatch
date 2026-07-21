namespace CareerMatch.API.DTOs
{
    // Receives the selected job. UserId is taken from the JWT.
    public class SavedJobRequest
    {
        public int JobId { get; set; }
    }
}
