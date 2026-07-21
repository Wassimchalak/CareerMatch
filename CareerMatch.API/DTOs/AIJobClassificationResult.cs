namespace CareerMatch.API.DTOs
{
    /// <summary>
    /// Root object returned by OpenAI.
    /// </summary>
    public class AIJobClassificationResult
    {
        public List<AIJobClassificationItem> Jobs
        {
            get;
            set;
        }
            = new();
    }
}