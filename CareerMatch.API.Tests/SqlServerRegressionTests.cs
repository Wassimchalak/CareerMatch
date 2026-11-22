using CareerMatch.API.Data;
using CareerMatch.API.DTOs;
using CareerMatch.API.Services;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CareerMatch.API.Tests;

/// <summary>
/// Real Dapper/SQL Server integration tests against a disposable LOCAL Docker
/// SQL Server database. No credentials or CV data are copied from production.
/// Execute only when CAREERMATCH_SQL_INTEGRATION_ENABLED=true and the connection
/// targets localhost's CareerMatchIntegrationTests database.
/// </summary>
[Trait("Category", "SqlIntegration")]
public sealed class SqlServerRegressionTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly DbConnectionFactory _factory;

    public SqlServerRegressionTests()
    {
        if (Environment.GetEnvironmentVariable("CAREERMATCH_SQL_INTEGRATION_ENABLED") != "true")
            throw new InvalidOperationException("SQL tests require explicit opt-in.");

        _connectionString = Environment.GetEnvironmentVariable("CAREERMATCH_TEST_SQL_CONNECTION")
            ?? throw new InvalidOperationException("Disposable test SQL connection is missing.");

        var parsed = new SqlConnectionStringBuilder(_connectionString);
        if (!parsed.DataSource.StartsWith("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            && !parsed.DataSource.StartsWith("localhost", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SQL tests can only use a local disposable server.");
        if (parsed.InitialCatalog != "CareerMatchIntegrationTests")
            throw new InvalidOperationException("Unexpected test database name.");

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _connectionString
        }).Build();
        _factory = new DbConnectionFactory(config);
    }

    public async Task InitializeAsync()
    {
        var master = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master", ConnectTimeout = 3
        };
        Exception? lastError = null;
        bool ready = false;
        for (int attempt = 0; attempt < 45; attempt++)
        {
            try
            {
                await using var connection = new SqlConnection(master.ConnectionString);
                await connection.OpenAsync();
                await connection.ExecuteAsync(@"
IF DB_ID('CareerMatchIntegrationTests') IS NULL
    CREATE DATABASE [CareerMatchIntegrationTests];");
                ready = true;
                break;
            }
            catch (SqlException error)
            {
                lastError = error;
                await Task.Delay(2000);
            }
        }
        if (!ready) throw new InvalidOperationException("Local SQL Server did not start.", lastError);

        using var db = _factory.CreateConnection();
        await db.ExecuteAsync(@"
IF OBJECT_ID('dbo.Jobs') IS NULL CREATE TABLE dbo.Jobs
(
    JobId int IDENTITY PRIMARY KEY,
    Title nvarchar(300) NOT NULL,
    CompanyName nvarchar(200) NOT NULL,
    Country nvarchar(100) NOT NULL,
    City nvarchar(100) NULL,
    JobUrl nvarchar(700) NOT NULL,
    DescriptionHash nvarchar(100) NULL
);
IF OBJECT_ID('dbo.CVs') IS NULL CREATE TABLE dbo.CVs
(
    CVId int IDENTITY PRIMARY KEY,
    UserId int NOT NULL,
    CVTextHash nvarchar(100) NOT NULL,
    UploadedAt datetime2 NOT NULL
);
IF OBJECT_ID('dbo.JobMatches') IS NULL CREATE TABLE dbo.JobMatches
(
    JobMatchId int IDENTITY PRIMARY KEY,
    UserId int NOT NULL,
    JobId int NOT NULL,
    CVTextHash nvarchar(100) NULL,
    DescriptionHash nvarchar(100) NULL,
    FinalScore decimal(5,2) NOT NULL,
    MatchExplanation nvarchar(max) NULL,
    Recommendation nvarchar(max) NULL,
    CreatedAt datetime2 NOT NULL
);
IF OBJECT_ID('dbo.SavedJobs') IS NULL CREATE TABLE dbo.SavedJobs
(
    SavedJobId int IDENTITY PRIMARY KEY,
    UserId int NOT NULL,
    JobId int NOT NULL,
    MatchScoreAtSave decimal(5,2) NULL,
    SavedMatchExplanation nvarchar(max) NULL,
    SavedRecommendation nvarchar(max) NULL,
    SavedAt datetime2 NOT NULL
);
IF OBJECT_ID('dbo.JobApplications') IS NULL CREATE TABLE dbo.JobApplications
(
    ApplicationId int IDENTITY PRIMARY KEY,
    UserId int NOT NULL,
    CVId int NULL,
    JobId int NOT NULL,
    ApplicationStatus nvarchar(40) NOT NULL,
    AppliedAt datetime2 NOT NULL
);
DELETE FROM dbo.SavedJobs;
DELETE FROM dbo.JobApplications;
DELETE FROM dbo.JobMatches;
DELETE FROM dbo.CVs;
DELETE FROM dbo.Jobs;
");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<int> SeedJobAsync(string descriptionHash = "current-description")
    {
        using var db = _factory.CreateConnection();
        return await db.ExecuteScalarAsync<int>(@"
INSERT INTO Jobs(Title,CompanyName,Country,City,JobUrl,DescriptionHash)
OUTPUT INSERTED.JobId
VALUES('Backend Engineer','Synthetic Test Employer','Lebanon','Beirut',
'https://example.invalid/test-job',@descriptionHash);", new { descriptionHash });
    }

    private async Task SeedCVAsync(int userId, string cvHash = "current-cv")
    {
        using var db = _factory.CreateConnection();
        await db.ExecuteAsync(@"
INSERT INTO CVs(UserId,CVTextHash,UploadedAt)
VALUES(@userId,@cvHash,SYSUTCDATETIME());", new { userId, cvHash });
    }

    private async Task SeedMatchAsync(int userId, int jobId, string descriptionHash,
        decimal score, int minutesAgo = 0, string cvHash = "current-cv")
    {
        using var db = _factory.CreateConnection();
        await db.ExecuteAsync(@"
INSERT INTO JobMatches
(UserId,JobId,CVTextHash,DescriptionHash,FinalScore,
MatchExplanation,Recommendation,CreatedAt)
VALUES(@userId,@jobId,@cvHash,@descriptionHash,@score,
'Isolated database integration fixture','Review test case',
DATEADD(MINUTE,-@minutesAgo,SYSUTCDATETIME()));",
            new { userId, jobId, cvHash, descriptionHash, score, minutesAgo });
    }

    [Fact]
    public async Task SavingJob_DoesNotReuseOldDescriptionScore()
    {
        var jobId = await SeedJobAsync("new-description");
        await SeedCVAsync(10);
        await SeedMatchAsync(10, jobId, "old-description", 96m);
        // Deliberately seed a different user's matching, current score:
        await SeedMatchAsync(20, jobId, "new-description", 88m);

        var service = new SavedJobService(_factory, null!);
        Assert.True(await service.SaveJobAsync(10, new SavedJobRequest { JobId = jobId }));
        var saved = Assert.Single(await service.GetSavedJobsAsync(10));
        Assert.Null(saved.MatchScoreAtSave);
        Assert.Empty(await service.GetSavedJobsAsync(20));
    }

    [Fact]
    public async Task SavingJob_ReusesOnlyCurrentOwnerCvAndDescriptionHashes()
    {
        var jobId = await SeedJobAsync();
        await SeedCVAsync(10);
        await SeedMatchAsync(10, jobId, "current-description", 78m);
        await SeedMatchAsync(20, jobId, "current-description", 99m);

        var service = new SavedJobService(_factory, null!);
        Assert.True(await service.SaveJobAsync(10, new SavedJobRequest { JobId = jobId }));
        Assert.Equal(78m, Assert.Single(await service.GetSavedJobsAsync(10)).MatchScoreAtSave);
        Assert.False(await service.UnsaveJobAsync(20, jobId));
        Assert.Single(await service.GetSavedJobsAsync(10));
    }

    [Fact]
    public async Task ApplicationHistory_SelectsOneLatestMatchAndDoesNotLeakOtherUser()
    {
        var jobId = await SeedJobAsync();
        await SeedMatchAsync(10, jobId, "current-description", 45m, minutesAgo: 6);
        await SeedMatchAsync(10, jobId, "current-description", 80m, minutesAgo: 1);
        await SeedMatchAsync(20, jobId, "current-description", 99m);

        using (var db = _factory.CreateConnection())
            await db.ExecuteAsync(@"
INSERT INTO JobApplications(UserId,JobId,ApplicationStatus,AppliedAt)
VALUES(10,@jobId,'Applied',SYSUTCDATETIME()),
      (20,@jobId,'Applied',SYSUTCDATETIME());", new { jobId });

        var service = new JobApplicationService(_factory);
        var ownerHistory = Assert.Single(await service.GetUserApplicationsAsync(10));
        Assert.Equal(80m, ownerHistory.MatchScore);
        Assert.Equal("Synthetic Test Employer", ownerHistory.CompanyName);
        var otherHistory = Assert.Single(await service.GetUserApplicationsAsync(20));
        Assert.Equal(99m, otherHistory.MatchScore);
    }

    [Fact]
    public async Task DeletingAnotherUsersApplication_IsRejectedWithoutDataLoss()
    {
        var jobId = await SeedJobAsync();
        int applicationId;
        using (var db = _factory.CreateConnection())
            applicationId = await db.ExecuteScalarAsync<int>(@"
INSERT INTO JobApplications(UserId,JobId,ApplicationStatus,AppliedAt)
OUTPUT INSERTED.ApplicationId
VALUES(10,@jobId,'Applied',SYSUTCDATETIME());", new { jobId });

        var service = new JobApplicationService(_factory);
        Assert.False(await service.DeleteApplicationAsync(20, applicationId));
        Assert.Single(await service.GetUserApplicationsAsync(10));
        Assert.Empty(await service.GetUserApplicationsAsync(20));
    }

    [Fact]
    public async Task CalculatingAnotherUsersSavedJob_IsRejectedBeforeAIRequest()
    {
        var jobId = await SeedJobAsync();
        using (var db = _factory.CreateConnection())
            await db.ExecuteAsync(@"
INSERT INTO SavedJobs(UserId,JobId,SavedAt)
VALUES(10,@jobId,SYSUTCDATETIME());", new { jobId });
        var service = new SavedJobService(_factory, null!);
        Assert.Null(await service.CalculateSavedJobScoreAsync(20, jobId));
    }
}
