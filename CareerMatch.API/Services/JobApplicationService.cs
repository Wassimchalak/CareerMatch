using CareerMatch.API.Data;
using CareerMatch.API.DTOs;
using CareerMatch.API.Models;
using Dapper;

namespace CareerMatch.API.Services
{
    public class JobApplicationService
    {
        private readonly DbConnectionFactory
            _dbConnectionFactory;

        public JobApplicationService(
            DbConnectionFactory dbConnectionFactory)
        {
            _dbConnectionFactory =
                dbConnectionFactory;
        }

        public async Task<JobApplicationResponse>
            ApplyForJobAsync(
                int userId,
                JobApplicationRequest request)
        {
            using var connection =
                _dbConnectionFactory
                    .CreateConnection();

            var cv =
                await connection
                    .QueryFirstOrDefaultAsync<CV>(
                        @"
                        SELECT TOP 1 *
                        FROM CVs
                        WHERE UserId = @UserId
                        ORDER BY UploadedAt DESC;
                        ",
                        new
                        {
                            UserId = userId
                        }
                    );

            if (cv == null)
            {
                return new JobApplicationResponse
                {
                    Success = false,
                    Message =
                        "You must upload a CV before applying."
                };
            }

            var job =
                await connection
                    .QueryFirstOrDefaultAsync<Job>(
                        @"
                        SELECT *
                        FROM Jobs
                        WHERE JobId = @JobId;
                        ",
                        new
                        {
                            request.JobId
                        }
                    );

            if (job == null)
            {
                return new JobApplicationResponse
                {
                    Success = false,
                    Message = "Job not found."
                };
            }

            var existingApplication =
                await connection
                    .QueryFirstOrDefaultAsync<JobApplication>(
                        @"
                        SELECT TOP 1 *
                        FROM JobApplications
                        WHERE UserId = @UserId
                          AND CVId = @CVId
                          AND JobId = @JobId
                        ORDER BY AppliedAt DESC;
                        ",
                        new
                        {
                            UserId = userId,
                            CVId = cv.CVId,
                            request.JobId
                        }
                    );

            if (existingApplication != null)
            {
                return new JobApplicationResponse
                {
                    Success = true,
                    Message =
                        "You already applied to this job.",
                    ApplicationId =
                        existingApplication.ApplicationId,
                    JobUrl = job.JobUrl
                };
            }

            DateTime appliedAt =
                DateTime.UtcNow;

            int applicationId =
                await connection
                    .ExecuteScalarAsync<int>(
                        @"
                        INSERT INTO JobApplications
                        (
                            UserId,
                            CVId,
                            JobId,
                            ApplicationStatus,
                            AppliedAt
                        )
                        OUTPUT INSERTED.ApplicationId
                        VALUES
                        (
                            @UserId,
                            @CVId,
                            @JobId,
                            @ApplicationStatus,
                            @AppliedAt
                        );
                        ",
                        new
                        {
                            UserId = userId,
                            CVId = cv.CVId,
                            request.JobId,
                            ApplicationStatus = "Applied",
                            AppliedAt = appliedAt
                        }
                    );

            return new JobApplicationResponse
            {
                Success = true,
                Message =
                    "Application saved successfully.",
                ApplicationId = applicationId,
                JobUrl = job.JobUrl
            };
        }

       public async Task<List<JobApplicationHistoryResponse>>
    GetUserApplicationsAsync(
        int userId)
{
    using var connection =
        _dbConnectionFactory
            .CreateConnection();

    var applications =
        await connection
            .QueryAsync<JobApplicationHistoryResponse>(
                @"
                SELECT
                    ja.ApplicationId,
                    j.JobId,
                    j.Title,
                    j.CompanyName,
                    j.Country,
                    j.City,
                    j.JobUrl,
                    ja.ApplicationStatus,
                    ja.AppliedAt,
                    jm.FinalScore AS MatchScore,
                    jm.MatchExplanation,
                    jm.Recommendation
                FROM JobApplications ja
                INNER JOIN Jobs j
                    ON ja.JobId = j.JobId
                LEFT JOIN JobMatches jm
                    ON jm.UserId = ja.UserId
                    AND jm.JobId = ja.JobId
                WHERE ja.UserId = @UserId
                ORDER BY ja.AppliedAt DESC;
                ",
                new
                {
                    UserId = userId
                }
            );

    return applications.ToList();
}
    }
}
