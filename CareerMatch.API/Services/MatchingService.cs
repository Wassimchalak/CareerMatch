using CareerMatch.API.Data;
using CareerMatch.API.DTOs;
using CareerMatch.API.Models;
using Dapper;

namespace CareerMatch.API.Services
{
    /// <summary>
    /// Handles candidate-to-job matching and match caching.
    ///
    /// Responsibilities:
    /// - Load the latest CV that actually has extracted skills.
    /// - Load the candidate's extracted skills.
    /// - Reuse valid cached matches.
    /// - Send only new or changed jobs to OpenAI.
    /// - Save only successful OpenAI results.
    /// </summary>
    public class MatchingService
    {
        // Used to create SQL Server connections.
        private readonly DbConnectionFactory _dbConnectionFactory;

        // Used to calculate job matches with OpenAI.
        private readonly AIService _aiService;

        public MatchingService(
            DbConnectionFactory dbConnectionFactory,
            AIService aiService)
        {
            _dbConnectionFactory = dbConnectionFactory;
            _aiService = aiService;
        }

        /// <summary>
        /// Calculates and saves match results for several jobs.
        ///
        /// Cache validation requires:
        /// - Same user
        /// - Same job
        /// - Same CVTextHash
        /// - Same DescriptionHash
        /// </summary>
        public async Task<Dictionary<int, AIMatchResult>>
            CalculateAndSaveMatchesAsync(
                int userId,
                IReadOnlyCollection<Job> jobs,
                JobSearchRequest request)
        {
            var result =
                new Dictionary<int, AIMatchResult>();

            // Return immediately when there are no jobs.
            if (jobs.Count == 0)
                return result;

            using var connection =
                _dbConnectionFactory.CreateConnection();

            /*
 * Load the user's latest uploaded CV.
 *
 * Do not filter by extracted skills here.
 * We first need to distinguish between:
 * 1. No CV uploaded.
 * 2. CV uploaded, but skill extraction failed.
 */
var cv =
    await connection.QueryFirstOrDefaultAsync<CV>(
        @"
        SELECT TOP 1
            CVId,
            UserId,
            PrimaryRole,
            CVTextHash,
            UploadedAt
        FROM CVs
        WHERE UserId = @UserId
        ORDER BY UploadedAt DESC;
        ",
        new
        {
            UserId = userId
        }
    );

// No CV has been uploaded for this user.
if (cv == null)
{
    throw new InvalidOperationException(
        "Please upload your CV before calculating your job match."
    );
}

// Load the skills extracted from the latest uploaded CV.
var cvSkills =
    (
        await connection.QueryAsync<AIExtractedSkill>(
            @"
            SELECT
                s.SkillName,
                ecvs.YearsOfExperience
            FROM ExtractedCVSkills ecvs
            INNER JOIN Skills s
                ON ecvs.SkillId = s.SkillId
            WHERE ecvs.CVId = @CVId
            ORDER BY
                ecvs.YearsOfExperience DESC,
                s.SkillName;
            ",
            new
            {
                CVId = cv.CVId
            }
        )
    ).ToList();

// A CV exists, but its skills were not extracted successfully.
if (cvSkills.Count == 0)
{
    throw new InvalidOperationException(
        "Your CV could not be analyzed. Please upload it again."
    );
}
            

            // Get unique job IDs for loading cached results.
            var jobIds = jobs
                .Select(job => job.JobId)
                .Distinct()
                .ToList();

            /*
             * Load all previous matches for these jobs.
             *
             * We do not filter by CVId because the same CV content can be
             * uploaded again and receive a different CVId.
             */
            var cachedMatches =
                (
                    await connection.QueryAsync<CachedMatchData>(
                        @"
                        SELECT
                            JobMatchId,
                            CVId,
                            JobId,
                            CVTextHash,
                            DescriptionHash,
                            FinalScore,
                            MatchExplanation,
                            Recommendation,
                            CreatedAt
                        FROM JobMatches
                        WHERE UserId = @UserId
                          AND JobId IN @JobIds
                        ORDER BY CreatedAt DESC;
                        ",
                        new
                        {
                            UserId = userId,
                            JobIds = jobIds
                        }
                    )
                ).ToList();

            /*
             * Keep only cache entries that:
             * - Were calculated using the same CV content.
             * - Do not represent a previous failed calculation.
             */
            var cachedMatchesByJobId =
                cachedMatches
                    .Where(cachedMatch =>
                        !string.IsNullOrWhiteSpace(
                            cv.CVTextHash
                        )
                        &&
                        string.Equals(
                            cachedMatch.CVTextHash,
                            cv.CVTextHash,
                            StringComparison.OrdinalIgnoreCase
                        )
                        &&
                        IsSuccessfulCachedMatch(
                            cachedMatch
                        )
                    )
                    .GroupBy(cachedMatch =>
                        cachedMatch.JobId
                    )
                    .ToDictionary(
                        group => group.Key,
                        group => group.First()
                    );

            // Jobs that require a new OpenAI calculation.
            var jobsToMatch = new List<Job>();

            foreach (var job in jobs)
            {
                bool validCacheExists =
                    cachedMatchesByJobId.TryGetValue(
                        job.JobId,
                        out var cachedMatch
                    )
                    &&
                    !string.IsNullOrWhiteSpace(
                        job.DescriptionHash
                    )
                    &&
                    string.Equals(
                        cachedMatch!.DescriptionHash,
                        job.DescriptionHash,
                        StringComparison.OrdinalIgnoreCase
                    );

                if (validCacheExists && cachedMatch != null)
                {
                    // Reuse the cached match.
                    result[job.JobId] =
                        new AIMatchResult
                        {
                            JobId = job.JobId,
                            MatchScore =
                                cachedMatch.FinalScore,
                            MatchExplanation =
                                cachedMatch.MatchExplanation
                                ?? string.Empty,
                            Recommendation =
                                cachedMatch.Recommendation
                                ?? string.Empty
                        };
                }
                else
                {
                    // This job is new or its description changed.
                    jobsToMatch.Add(job);
                }
            }

            // No OpenAI request is needed when all matches are cached.
            if (jobsToMatch.Count == 0)
                return result;

            List<AIMatchResult> aiMatches;

            try
            {
                /*
                 * Send one request containing:
                 * - CV primary role
                 * - Extracted CV skills
                 * - Search preferences
                 * - Only jobs without a valid cache
                 */
                aiMatches =
                    await _aiService.GenerateJobMatchesAsync(
                        cv.PrimaryRole ?? string.Empty,
                        cvSkills,
                        request,
                        jobsToMatch
                    );
            }
            catch (Exception ex)
            {
                // Show the real error in the API terminal.
                Console.WriteLine(
                    $"OPENAI MATCH ERROR: {ex}"
                );

                aiMatches =
                    new List<AIMatchResult>();
            }

            // Only accept results belonging to requested jobs.
            var validJobIds = jobsToMatch
                .Select(job => job.JobId)
                .ToHashSet();

            var aiMatchesByJobId =
                aiMatches
                    .Where(aiMatch =>
                        validJobIds.Contains(
                            aiMatch.JobId
                        )
                    )
                    .GroupBy(aiMatch =>
                        aiMatch.JobId
                    )
                    .ToDictionary(
                        group => group.Key,
                        group => group.First()
                    );

            foreach (var job in jobsToMatch)
            {
                /*
                 * Save only when OpenAI returned a real match
                 * for this exact JobId.
                 */
                if (aiMatchesByJobId.TryGetValue(
                        job.JobId,
                        out var aiMatch))
                {
                    var matchResult =
                        new AIMatchResult
                        {
                            JobId = job.JobId,

                            MatchScore = Math.Clamp(
                                aiMatch.MatchScore,
                                0,
                                100
                            ),

                            MatchExplanation =
                                aiMatch.MatchExplanation
                                ?? string.Empty,

                            Recommendation =
                                aiMatch.Recommendation
                                ?? string.Empty,

                            MatchedSkills =
                                aiMatch.MatchedSkills
                                ?? new List<string>(),

                            MissingSkills =
                                aiMatch.MissingSkills
                                ?? new List<string>()
                        };

                    result[job.JobId] =
                        matchResult;

                    // Save only a successful AI response.
                    await SaveMatchAsync(
                        connection,
                        userId,
                        cv.CVId,
                        job.JobId,
                        cv.CVTextHash,
                        job.DescriptionHash,
                        matchResult
                    );
                }
                else
                {
                    /*
                     * Return a temporary failure result.
                     * Do not save it, so the next request can try again.
                     */
                    result[job.JobId] =
                        new AIMatchResult
                        {
                            JobId = job.JobId,
                            MatchScore = 0,
                            MatchExplanation =
                                "Match calculation failed.",
                            Recommendation =
                                "Try calculating the match again."
                        };
                }
            }

            return result;
        }

        /// <summary>
        /// Saves or updates a successful cached match.
        /// </summary>
        private static async Task SaveMatchAsync(
            System.Data.IDbConnection connection,
            int userId,
            int cvId,
            int jobId,
            string? cvTextHash,
            string? descriptionHash,
            AIMatchResult matchResult)
        {
            await connection.ExecuteAsync(
                @"
                IF EXISTS
                (
                    SELECT 1
                    FROM JobMatches
                    WHERE UserId = @UserId
                      AND JobId = @JobId
                      AND CVTextHash = @CVTextHash
                      AND DescriptionHash = @DescriptionHash
                )
                BEGIN
                    UPDATE JobMatches
                    SET
                        CVId = @CVId,
                        FinalScore = @FinalScore,
                        MatchExplanation = @MatchExplanation,
                        Recommendation = @Recommendation,
                        CreatedAt = @CreatedAt
                    WHERE UserId = @UserId
                      AND JobId = @JobId
                      AND CVTextHash = @CVTextHash
                      AND DescriptionHash = @DescriptionHash;
                END
                ELSE
                BEGIN
                    INSERT INTO JobMatches
                    (
                        UserId,
                        CVId,
                        JobId,
                        CVTextHash,
                        DescriptionHash,
                        FinalScore,
                        MatchExplanation,
                        Recommendation,
                        CreatedAt
                    )
                    VALUES
                    (
                        @UserId,
                        @CVId,
                        @JobId,
                        @CVTextHash,
                        @DescriptionHash,
                        @FinalScore,
                        @MatchExplanation,
                        @Recommendation,
                        @CreatedAt
                    );
                END
                ",
                new
                {
                    UserId = userId,
                    CVId = cvId,
                    JobId = jobId,
                    CVTextHash = cvTextHash,
                    DescriptionHash = descriptionHash,
                    FinalScore =
                        matchResult.MatchScore,
                    MatchExplanation =
                        matchResult.MatchExplanation,
                    Recommendation =
                        matchResult.Recommendation,
                    CreatedAt = DateTime.Now
                }
            );
        }

        /// <summary>
        /// Prevents old failed match rows from being treated as valid cache.
        /// </summary>
        private static bool IsSuccessfulCachedMatch(
            CachedMatchData cachedMatch)
        {
            string explanation =
                cachedMatch.MatchExplanation
                ?? string.Empty;

            bool failed =
                explanation.Contains(
                    "failed",
                    StringComparison.OrdinalIgnoreCase
                )
                ||
                explanation.Contains(
                    "did not return",
                    StringComparison.OrdinalIgnoreCase
                )
                ||
                explanation.Contains(
                    "no candidate skills supplied",
                    StringComparison.OrdinalIgnoreCase
                )
                ||
                explanation.Contains(
                    "no listed skills",
                    StringComparison.OrdinalIgnoreCase
                );

            return !failed;
        }

        /// <summary>
        /// Private Dapper mapping class for cached JobMatches rows.
        /// </summary>
        private class CachedMatchData
        {
            public int JobMatchId { get; set; }

            public int CVId { get; set; }

            public int JobId { get; set; }

            public string? CVTextHash { get; set; }

            public string? DescriptionHash { get; set; }

            public decimal FinalScore { get; set; }

            public string? MatchExplanation { get; set; }

            public string? Recommendation { get; set; }

            public DateTime CreatedAt { get; set; }
        }
    }
}