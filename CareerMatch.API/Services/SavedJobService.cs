using CareerMatch.API.Data;
using CareerMatch.API.DTOs;
using Dapper;

namespace CareerMatch.API.Services
{
    public class SavedJobService
    {
        private readonly DbConnectionFactory
            _dbConnectionFactory;

        public SavedJobService(
            DbConnectionFactory dbConnectionFactory)
        {
            _dbConnectionFactory =
                dbConnectionFactory;
        }

        public async Task<bool> SaveJobAsync(
            int userId,
            SavedJobRequest request)
        {
            using var connection =
                _dbConnectionFactory
                    .CreateConnection();

            int jobExists =
                await connection
                    .ExecuteScalarAsync<int>(
                        @"
                        SELECT COUNT(1)
                        FROM Jobs
                        WHERE JobId = @JobId;
                        ",
                        new
                        {
                            request.JobId
                        }
                    );

            if (jobExists == 0)
            {
                return false;
            }

            int alreadySaved =
                await connection
                    .ExecuteScalarAsync<int>(
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
                await connection
                    .QueryFirstOrDefaultAsync(
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
                    MatchScoreAtSave =
                        match?.FinalScore,
                    SavedMatchExplanation =
                        match?.MatchExplanation,
                    SavedAt = DateTime.UtcNow
                }
            );

            return true;
        }

        public async Task<List<SavedJobResponse>>
            GetSavedJobsAsync(
                int userId)
        {
            using var connection =
                _dbConnectionFactory
                    .CreateConnection();

            var savedJobs =
                await connection
                    .QueryAsync<SavedJobResponse>(
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
                        new
                        {
                            UserId = userId
                        }
                    );

            return savedJobs.ToList();
        }

        public async Task<bool> UnsaveJobAsync(
            int userId,
            int jobId)
        {
            using var connection =
                _dbConnectionFactory
                    .CreateConnection();

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
