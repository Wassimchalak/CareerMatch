using CareerMatch.API.Data;
using CareerMatch.API.DTOs;
using CareerMatch.API.Models;
using Dapper;

namespace CareerMatch.API.Services
{
    public class MatchingService
    {
        private readonly DbConnectionFactory _dbConnectionFactory;
        private readonly AIService _aiService;

        public MatchingService(
            DbConnectionFactory dbConnectionFactory,
            AIService aiService)
        {
            _dbConnectionFactory = dbConnectionFactory;
            _aiService = aiService;
        }

        public async Task<Dictionary<int, AIMatchResult>>
            CalculateAndSaveMatchesAsync(
                int userId,
                IReadOnlyCollection<Job> jobs,
                JobSearchRequest request,
                bool forceRefresh = false)
        {
            var result =
                new Dictionary<int, AIMatchResult>();

            if (jobs.Count == 0)
            {
                return result;
            }

            using var connection =
                _dbConnectionFactory.CreateConnection();

            var cv =
                await connection.QueryFirstOrDefaultAsync<CV>(
                    @"
                    SELECT TOP 1
                        cv.CVId,
                        cv.UserId,
                        cv.PrimaryRole,
                        cv.CVTextHash,
                        cv.UploadedAt
                    FROM CVs cv
                    WHERE cv.UserId = @UserId
                    ORDER BY
                        cv.UploadedAt DESC,
                        cv.CVId DESC;
                    ",
                    new
                    {
                        UserId = userId
                    }
                );

            if (cv == null)
            {
                throw new InvalidOperationException(
                    "Please upload a CV before calculating your match score."
                );
            }

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

            if (cvSkills.Count == 0)
            {
                foreach (var job in jobs)
                {
                    result[job.JobId] =
                        new AIMatchResult
                        {
                            JobId = job.JobId,
                            MatchScore = 0,
                            MatchExplanation =
                                "The latest uploaded CV has no extracted skills.",
                            Recommendation =
                                "Upload the CV again so its skills can be extracted."
                        };
                }

                return result;
            }

            var jobIds =
                jobs
                    .Select(job => job.JobId)
                    .Distinct()
                    .ToList();

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

            var jobsToMatch =
                new List<Job>();

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

                if (
                    !forceRefresh &&
                    validCacheExists &&
                    cachedMatch != null
                )
                {
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
                    jobsToMatch.Add(job);
                }
            }

            if (jobsToMatch.Count == 0)
            {
                return result;
            }

            List<AIMatchResult> aiMatches;

            try
            {
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
                Console.WriteLine(
                    $"OPENAI MATCH ERROR: {ex}"
                );

                aiMatches =
                    new List<AIMatchResult>();
            }

            var validJobIds =
                jobsToMatch
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
                if (
                    aiMatchesByJobId.TryGetValue(
                        job.JobId,
                        out var aiMatch
                    )
                )
                {
                    var matchResult =
                        new AIMatchResult
                        {
                            JobId = job.JobId,
                            MatchScore =
                                Math.Clamp(
                                    aiMatch.MatchScore,
                                    0,
                                    100
                                ),
                            MatchExplanation =
                                aiMatch.MatchExplanation
                                ?? string.Empty,
                            Recommendation =
                                aiMatch.Recommendation
                                ?? string.Empty
                        };

                    result[job.JobId] =
                        matchResult;

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
                    CreatedAt = DateTime.UtcNow
                }
            );
        }

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
                )
                ||
                explanation.Contains(
                    "no extracted skills",
                    StringComparison.OrdinalIgnoreCase
                );

            return !failed;
        }

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