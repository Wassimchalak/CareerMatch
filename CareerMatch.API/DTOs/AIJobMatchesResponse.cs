namespace CareerMatch.API.DTOs
{
    public class AIJobMatchesResponse
    {
        public List<AIMatchResult> Matches { get; set; }
            = new();
    }
}