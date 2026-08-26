namespace CareerMatch.API.DTOs
{

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