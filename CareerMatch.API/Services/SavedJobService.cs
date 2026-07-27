using CareerMatch.API.Data;
using CareerMatch.API.DTOs;
using CareerMatch.API.Models;
using Dapper;

namespace CareerMatch.API.Services
{
    public class SavedJobService
    {
        private readonly DbConnectionFactory _dbConnectionFactory;
        private readonly MatchingService _matchingService;

        public SavedJobService(
            DbConnectionFactory dbConnectionFactory,
            MatchingService matchingService)
        {
            _dbConnectionFactory = dbConnectionFactory;
            _matchingService = matchingService;
        }

        public async Task<bool> SaveJobAsync(
            int userId,
            SavedJobRequest request)
        {
            using var connection =
                _dbConnectionFactory.CreateConnection();

            int jobExists =
                await connection.ExecuteScalarAsync<int>(
                    @"
                    SELECT COUNT(1)
                    FROM Jobs
                    WHERE JobId = @JobId;
                    ",
                    new { request.JobId }
                );

            if (jobExists == 0)
            {
                return false;
            }

            int alreadySaved =
                await connection.ExecuteScalarAsync<int>(
                    @"
                    SELECT COUNT(1)
                    FROM SavedJobs
                    WHERE UserId = @UserId
                      AND JobId = @JobId;
                    ",
                    new
                    {
                        UserId = userId,
                        request.JobId
                    }
                );

            if (alreadySaved > 0)
            {
                return true;
            }

            var match =
                await connection.QueryFirstOrDefaultAsync(
                    @"
                    SELECT TOP 1
                        FinalScore,
                        MatchExplanation
                    FROM JobMatches
                    WHERE UserId = @UserId
                      AND JobId = @JobId
                    ORDER BY CreatedAt DESC;
                    ",
                    new
                    {
                        UserId = userId,
                        request.JobId
                    }
                );

            await connection.ExecuteAsync(
                @"
                INSERT INTO SavedJobs
                (
                    UserId,
                    JobId,
                    MatchScoreAtSave,
                    SavedMatchExplanation,
                    SavedAt
                )
                VALUES
                (
                    @UserId,
                    @JobId,
                    @MatchScoreAtSave,
                    @SavedMatchExplanation,
                    @SavedAt
                );
                ",
                new
                {
                    UserId = userId,
                    request.JobId,
                    MatchScoreAtSave = match?.FinalScore,
                    SavedMatchExplanation = match?.MatchExplanation,
                    SavedAt = DateTime.UtcNow
                }
            );

            return true;
        }

        public async Task<List<SavedJobResponse>>
            GetSavedJobsAsync(int userId)
        {
            using var connection =
                _dbConnectionFactory.CreateConnection();

            var savedJobs =
                await connection.QueryAsync<SavedJobResponse>(
                    @"
                    SELECT
                        sj.SavedJobId,
                        j.JobId,
                        j.Title,
                        j.CompanyName,
                        j.Country,
                        j.City,
                        j.JobUrl,
                        sj.MatchScoreAtSave,
                        sj.SavedMatchExplanation,
                        sj.SavedAt
                    FROM SavedJobs sj
                    INNER JOIN Jobs j
                        ON sj.JobId = j.JobId
                    WHERE sj.UserId = @UserId
                    ORDER BY sj.SavedAt DESC;
                    ",
                    new { UserId = userId }
                );

            return savedJobs.ToList();
        }

        public async Task<SavedJobScoreResponse?>
            CalculateSavedJobScoreAsync(
                int userId,
                int jobId)
        {
            return await CalculateOrRefreshSavedJobScoreAsync(
                userId,
                jobId,
                forceRefresh: false
            );
        }

        public async Task<SavedJobScoreResponse?>
            RefreshSavedJobScoreAsync(
                int userId,
                int jobId)
        {
            return await CalculateOrRefreshSavedJobScoreAsync(
                userId,
                jobId,
                forceRefresh: true
            );
        }

        private async Task<SavedJobScoreResponse?>
            CalculateOrRefreshSavedJobScoreAsync(
                int userId,
                int jobId,
                bool forceRefresh)
        {
            using var connection =
                _dbConnectionFactory.CreateConnection();

            var job =
                await connection.QueryFirstOrDefaultAsync<Job>(
                    @"
                    SELECT
                        j.JobId,
                        j.ExternalJobId,
                        j.Title,
                        j.CompanyName,
                        j.Country,
                        j.City,
                        j.Description,
                        j.DescriptionHash,
                        j.JobUrl,
                        j.EmploymentType,
                        j.WorkMode,
                        j.PostedDate,
                        j.CreatedAt,
                        j.PrimaryRole
                    FROM SavedJobs sj
                    INNER JOIN Jobs j
                        ON sj.JobId = j.JobId
                    WHERE sj.UserId = @UserId
                      AND sj.JobId = @JobId;
                    ",
                    new
                    {
                        UserId = userId,
                        JobId = jobId
                    }
                );

            if (job == null)
            {
                return null;
            }

            var matchingRequest =
                new JobSearchRequest
                {
                    Country =
                        job.Country ?? string.Empty,

                    City =
                        job.City,

                    Role =
                        string.IsNullOrWhiteSpace(
                            job.PrimaryRole
                        )
                            ? job.Title
                            : job.PrimaryRole,

                    WorkType =
                        job.WorkMode ?? string.Empty,

                    EmploymentType =
                        job.EmploymentType ?? string.Empty
                };

            Dictionary<int, AIMatchResult> matches =
                await _matchingService
                    .CalculateAndSaveMatchesAsync(
                        userId,
                        new List<Job>
                        {
                            job
                        },
                        matchingRequest,
                        forceRefresh
                    );

            if (
                !matches.TryGetValue(
                    jobId,
                    out AIMatchResult? match
                )
            )
            {
                return null;
            }

            if (
                string.Equals(
                    match.MatchExplanation,
                    "Match calculation failed.",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return null;
            }

            await connection.ExecuteAsync(
                @"
                UPDATE SavedJobs
                SET
                    MatchScoreAtSave = @MatchScore,
                    SavedMatchExplanation = @MatchExplanation
                WHERE UserId = @UserId
                  AND JobId = @JobId;
                ",
                new
                {
                    UserId = userId,
                    JobId = jobId,
                    MatchScore = match.MatchScore,
                    MatchExplanation =
                        match.MatchExplanation
                }
            );

            return new SavedJobScoreResponse
            {
                JobId = jobId,
                MatchScore = match.MatchScore,
                MatchExplanation =
                    match.MatchExplanation,
                Recommendation =
                    match.Recommendation
            };
        }

        public async Task<bool> UnsaveJobAsync(
            int userId,
            int jobId)
        {
            using var connection =
                _dbConnectionFactory.CreateConnection();

            int rowsAffected =
                await connection.ExecuteAsync(
                    @"
                    DELETE FROM SavedJobs
                    WHERE UserId = @UserId
                      AND JobId = @JobId;
                    ",
                    new
                    {
                        UserId = userId,
                        JobId = jobId
                    }
                );

            return rowsAffected > 0;
        }
    }
}