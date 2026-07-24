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

            // The latest CV is optional. A user may continue to the external
            // application page and upload a CV there instead.
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
                    Message = "Job not found.",
                    HasCV = cv != null
                };
            }

            // One CareerMatch application is kept per user and job.
            // This also reuses an application that was originally created
            // without a CV.
            var existingApplication =
                await connection
                    .QueryFirstOrDefaultAsync<JobApplication>(
                        @"
                        SELECT TOP 1 *
                        FROM JobApplications
                        WHERE UserId = @UserId
                          AND JobId = @JobId
                        ORDER BY AppliedAt DESC;
                        ",
                        new
                        {
                            UserId = userId,
                            request.JobId
                        }
                    );

            if (existingApplication != null)
            {
                // If the application was created without a CV but the user
                // uploaded one later, attach the latest CV now.
                if (existingApplication.CVId == null &&
                    cv != null)
                {
                    await connection.ExecuteAsync(
                        @"
                        UPDATE JobApplications
                        SET CVId = @CVId
                        WHERE ApplicationId = @ApplicationId;
                        ",
                        new
                        {
                            CVId = cv.CVId,
                            existingApplication.ApplicationId
                        }
                    );
                }

                return new JobApplicationResponse
                {
                    Success = true,
                    Message =
                        "You already opened this job application.",
                    ApplicationId =
                        existingApplication.ApplicationId,
                    JobUrl = job.JobUrl,
                    HasCV = cv != null
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
                            CVId = cv?.CVId,
                            request.JobId,
                            ApplicationStatus = "Applied",
                            AppliedAt = appliedAt
                        }
                    );

            return new JobApplicationResponse
            {
                Success = true,
                Message = cv == null
                    ? "Application opened. You can upload your CV on the job website."
                    : "Application saved successfully.",
                ApplicationId = applicationId,
                JobUrl = job.JobUrl,
                HasCV = cv != null
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
public async Task<bool> DeleteApplicationAsync(
    int userId,
    int applicationId)
{
    using var connection =
        _dbConnectionFactory.CreateConnection();

    connection.Open();

    using var transaction =
        connection.BeginTransaction();

    try
    {
        // Confirm that the application exists and belongs
        // to the authenticated user.
        int applicationExists =
            await connection.ExecuteScalarAsync<int>(
                @"
                SELECT COUNT(1)
                FROM JobApplications
                WHERE ApplicationId = @ApplicationId
                  AND UserId = @UserId;
                ",
                new
                {
                    ApplicationId = applicationId,
                    UserId = userId
                },
                transaction
            );

        if (applicationExists == 0)
        {
            transaction.Rollback();
            return false;
        }

        // Delete all generated interview-question records,
        // if any exist.
        await connection.ExecuteAsync(
            @"
            DELETE FROM GeneratedInterviewQuestions
            WHERE ApplicationId = @ApplicationId;
            ",
            new
            {
                ApplicationId = applicationId
            },
            transaction
        );

        // Delete all generated cover-letter records,
        // if any exist.
        await connection.ExecuteAsync(
            @"
            DELETE FROM GeneratedCoverLetters
            WHERE ApplicationId = @ApplicationId;
            ",
            new
            {
                ApplicationId = applicationId
            },
            transaction
        );

        // Delete all generated CV records,
        // if any exist.
        await connection.ExecuteAsync(
            @"
            DELETE FROM GeneratedCVs
            WHERE ApplicationId = @ApplicationId;
            ",
            new
            {
                ApplicationId = applicationId
            },
            transaction
        );

        // Delete the application itself.
        int deletedRows =
            await connection.ExecuteAsync(
                @"
                DELETE FROM JobApplications
                WHERE ApplicationId = @ApplicationId
                  AND UserId = @UserId;
                ",
                new
                {
                    ApplicationId = applicationId,
                    UserId = userId
                },
                transaction
            );

        transaction.Commit();

        return deletedRows > 0;
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
    }
}