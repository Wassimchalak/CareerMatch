using CareerMatch.API.Data;
using CareerMatch.API.DTOs;
using CareerMatch.API.Models;
using Dapper;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CareerMatch.API.Services
{
    /// <summary>
    /// Searches LinkedIn jobs through the Bebity Apify actor, persists jobs,
    /// and keeps the existing CV/job matching flow unchanged.
    /// </summary>
    public class JobSearchService
    {
        private const string BebityActorEndpoint =
            "https://api.apify.com/v2/actors/bebity~linkedin-jobs-scraper/run-sync-get-dataset-items";

        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly DbConnectionFactory _dbConnectionFactory;
        private readonly MatchingService _matchingService;

        public JobSearchService(
            HttpClient httpClient,
            IConfiguration configuration,
            DbConnectionFactory dbConnectionFactory,
            MatchingService matchingService)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _dbConnectionFactory = dbConnectionFactory;
            _matchingService = matchingService;
        }

        public async Task<List<JobSearchResponse>> SearchJobsAsync(
            JobSearchRequest request)
        {
            ValidateSearchRequest(request);

            List<Job> jobs = await SearchBebityAsync(request);

            if (jobs.Count == 0)
            {
                return new List<JobSearchResponse>();
            }

            // Remove duplicates conservatively.
            // Different companies are never merged. Similar titles alone are
            // never enough. The content-based rule applies only when company,
            // title, description, location, employment type and work mode all
            // match after normalization.
            jobs = jobs
                .GroupBy(job => job.ExternalJobId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .GroupBy(job => string.Join("|",
                    NormalizeDuplicateField(job.CompanyName),
                    NormalizeDuplicateField(job.Title),
                    NormalizeDuplicateField(job.Description),
                    NormalizeDuplicateField(job.Country),
                    NormalizeDuplicateField(job.City),
                    NormalizeDuplicateField(job.EmploymentType),
                    NormalizeDuplicateField(job.WorkMode)))
                .Select(group => group.First())
                .ToList();

            foreach (Job job in jobs)
            {
                await SaveOrUpdateJobAsync(job);
            }

            return jobs
                .Select(job => new JobSearchResponse
                {
                    JobId = job.JobId,
                    ExternalJobId = job.ExternalJobId,
                    Title = job.Title,
                    CompanyName = job.CompanyName,
                    Country = job.Country,
                    City = job.City,
                    Description = job.Description,
                    JobUrl = job.JobUrl,
                    EmploymentType = job.EmploymentType,
                    WorkMode = job.WorkMode,
                    PostedDate = job.PostedDate,
                    MatchScore = null,
                    MatchExplanation = null,
                    Recommendation = null,
                    MatchStatus = "Pending"
                })
                .ToList();
        }

        /// <summary>
        /// Existing CareerMatch CV/job matching flow. This remains unchanged.
        /// </summary>
        public async Task<List<JobSearchResponse>> CalculateMatchesAsync(
            int authenticatedUserId,
            CalculateMatchesRequest request)
        {
            if (request.JobIds == null ||
                request.JobIds.Count == 0)
            {
                return new List<JobSearchResponse>();
            }

            using var connection =
                _dbConnectionFactory.CreateConnection();

            List<Job> jobs =
                (
                    await connection.QueryAsync<Job>(
                        @"
                        SELECT
                            JobId,
                            ExternalJobId,
                            Title,
                            CompanyName,
                            Country,
                            City,
                            Description,
                            DescriptionHash,
                            JobUrl,
                            EmploymentType,
                            WorkMode,
                            PostedDate,
                            CreatedAt,
                            PrimaryRole
                        FROM Jobs
                        WHERE JobId IN @JobIds;
                        ",
                        new
                        {
                            JobIds = request.JobIds
                        }
                    )
                ).ToList();

            if (jobs.Count == 0)
            {
                return new List<JobSearchResponse>();
            }

            var searchRequest =
                new JobSearchRequest
                {
                    Country = request.Country,
                    City = request.City,
                    Role = request.Role,
                    WorkType = request.WorkType,
                    EmploymentType = request.EmploymentType
                };

            Dictionary<int, AIMatchResult> matches =
                await _matchingService
                    .CalculateAndSaveMatchesAsync(
                        authenticatedUserId,
                        jobs,
                        searchRequest
                    );

            return jobs
                .Select(job =>
                {
                    bool found =
                        matches.TryGetValue(
                            job.JobId,
                            out AIMatchResult? match
                        );

                    return new JobSearchResponse
                    {
                        JobId = job.JobId,
                        ExternalJobId = job.ExternalJobId,
                        Title = job.Title,
                        CompanyName = job.CompanyName,
                        Country = job.Country,
                        City = job.City,
                        Description = job.Description,
                        JobUrl = job.JobUrl,
                        EmploymentType = job.EmploymentType,
                        WorkMode = job.WorkMode,
                        PostedDate = job.PostedDate,
                        MatchScore = found ? match!.MatchScore : null,
                        MatchExplanation = found
                            ? match!.MatchExplanation
                            : "Match analysis was not returned.",
                        Recommendation = found
                            ? match!.Recommendation
                            : "Try calculating the match again.",
                        MatchStatus = found ? "Completed" : "Failed"
                    };
                })
                .OrderByDescending(job => job.MatchScore)
                .ToList();
        }

        private async Task<List<Job>> SearchBebityAsync(
            JobSearchRequest request)
        {
            var jobs = new List<Job>();

            string apiToken =
                _configuration["Apify:ApiToken"]
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(apiToken))
            {
                throw new InvalidOperationException(
                    "Apify API token is missing. Configure Apify:ApiToken."
                );
            }

            string contractTypeCode =
                GetBebityContractTypeCode(request.EmploymentType);

            string workTypeCode =
                GetBebityWorkTypeCode(request.WorkType);

            string location = string.IsNullOrWhiteSpace(request.City)
                ? request.Country.Trim()
                : $"{request.Country.Trim()},{request.City.Trim()}";

            var actorInput = new
            {
                companyProfile = false,
                enrichCompany = false,
                contractTypes = new[] { contractTypeCode },
                locations = new[] { location },
                publishedAt = "r2592000", // Past Month
                rows = 10,
                titles = new[] { request.Role.Trim() },
                workTypes = new[] { workTypeCode }
            };

            string url =
                $"{BebityActorEndpoint}?token={Uri.EscapeDataString(apiToken)}";

            using var httpRequest =
                new HttpRequestMessage(HttpMethod.Post, url);

            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(actorInput),
                Encoding.UTF8,
                "application/json"
            );

            using HttpResponseMessage response =
                await _httpClient.SendAsync(httpRequest);

            string jsonResponse =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Bebity job search failed with HTTP {(int)response.StatusCode}."
                );
            }

            using JsonDocument json =
                JsonDocument.Parse(jsonResponse);

            if (json.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException(
                    "Bebity response was not a dataset-item array."
                );
            }

            foreach (JsonElement item in json.RootElement.EnumerateArray())
            {
                string title = GetString(item, "title").Trim();
                string companyName = GetString(item, "companyName").Trim();
                string description = GetString(item, "description");
                string linkedInJobUrl = GetString(item, "jobUrl").Trim();
                string applyUrl = GetString(item, "applyUrl").Trim();
                string contractType = NormalizeEmploymentType(
                    GetString(item, "contractType")
                );
                string workType = NormalizeWorkMode(
                    GetString(item, "workType")
                );

                if (string.IsNullOrWhiteSpace(title) ||
                    string.IsNullOrWhiteSpace(linkedInJobUrl) ||
                    string.IsNullOrWhiteSpace(contractType) ||
                    string.IsNullOrWhiteSpace(workType))
                {
                    continue;
                }

                string externalJobId =
                    CreateBebityExternalJobId(linkedInJobUrl);

                if (string.IsNullOrWhiteSpace(externalJobId))
                {
                    continue;
                }

                // CareerMatch's JobUrl is the destination opened when the user
                // applies. Prefer Bebity's direct apply URL, then LinkedIn URL.
                string destinationUrl =
                    !string.IsNullOrWhiteSpace(applyUrl)
                        ? applyUrl
                        : linkedInJobUrl;

                jobs.Add(
                    new Job
                    {
                        ExternalJobId = externalJobId,
                        Title = title,
                        CompanyName = companyName,
                        Country = request.Country.Trim(),
                        City = string.IsNullOrWhiteSpace(request.City)
                            ? null
                            : request.City.Trim(),
                        Description = description,
                        DescriptionHash = CreateDescriptionHash(description),
                        ClassificationHash = null,
                        ClassifiedAt = null,
                        JobUrl = destinationUrl,
                        EmploymentType = contractType,
                        WorkMode = workType,
                        PostedDate = GetBebityPostedDate(item),
                        CreatedAt = DateTime.UtcNow,
                        PrimaryRole = request.Role.Trim()
                    }
                );
            }

            return jobs;
        }

        private async Task SaveOrUpdateJobAsync(Job job)
        {
            using var connection =
                _dbConnectionFactory.CreateConnection();

            int? existingJobId =
                await connection.QueryFirstOrDefaultAsync<int?>(
                    @"
                    SELECT JobId
                    FROM Jobs
                    WHERE ExternalJobId = @ExternalJobId;
                    ",
                    new
                    {
                        job.ExternalJobId
                    }
                );

            if (existingJobId.HasValue)
            {
                job.JobId = existingJobId.Value;

                await connection.ExecuteAsync(
                    @"
                    UPDATE Jobs
                    SET
                        Title = @Title,
                        CompanyName = @CompanyName,
                        Country = @Country,
                        City = @City,
                        Description = @Description,
                        DescriptionHash = @DescriptionHash,
                        JobUrl = @JobUrl,
                        EmploymentType = @EmploymentType,
                        WorkMode = @WorkMode,
                        PostedDate = @PostedDate,
                        PrimaryRole = @PrimaryRole,
                        ClassificationHash = NULL,
                        ClassifiedAt = NULL
                    WHERE JobId = @JobId;
                    ",
                    job
                );

                return;
            }

            job.JobId =
                await connection.ExecuteScalarAsync<int>(
                    @"
                    INSERT INTO Jobs
                    (
                        ExternalJobId,
                        Title,
                        CompanyName,
                        Country,
                        City,
                        Description,
                        DescriptionHash,
                        JobUrl,
                        EmploymentType,
                        WorkMode,
                        PostedDate,
                        CreatedAt,
                        PrimaryRole,
                        ClassificationHash,
                        ClassifiedAt
                    )
                    OUTPUT INSERTED.JobId
                    VALUES
                    (
                        @ExternalJobId,
                        @Title,
                        @CompanyName,
                        @Country,
                        @City,
                        @Description,
                        @DescriptionHash,
                        @JobUrl,
                        @EmploymentType,
                        @WorkMode,
                        @PostedDate,
                        @CreatedAt,
                        @PrimaryRole,
                        NULL,
                        NULL
                    );
                    ",
                    job
                );
        }

        private static void ValidateSearchRequest(JobSearchRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Country) ||
                string.IsNullOrWhiteSpace(request.Role) ||
                string.IsNullOrWhiteSpace(request.WorkType) ||
                string.IsNullOrWhiteSpace(request.EmploymentType))
            {
                throw new ArgumentException(
                    "Country, role, work mode, and employment type are required."
                );
            }

            _ = GetBebityContractTypeCode(request.EmploymentType);
            _ = GetBebityWorkTypeCode(request.WorkType);
        }

        private static string GetBebityContractTypeCode(
            string employmentType)
        {
            return employmentType.Trim().ToLowerInvariant() switch
            {
                "full-time" => "F",
                "part-time" => "P",
                "contract" => "C",
                "internship" => "I",
                _ => throw new ArgumentException(
                    $"Unsupported employment type: {employmentType}"
                )
            };
        }

        private static string GetBebityWorkTypeCode(string workType)
        {
            return workType.Trim().ToLowerInvariant() switch
            {
                "on-site" => "1",
                "remote" => "2",
                "hybrid" => "3",
                _ => throw new ArgumentException(
                    $"Unsupported work mode: {workType}"
                )
            };
        }

        private static string NormalizeEmploymentType(string? value)
        {
            return value?.Trim().ToLowerInvariant() switch
            {
                "full-time" => "Full-time",
                "full time" => "Full-time",
                "part-time" => "Part-time",
                "part time" => "Part-time",
                "contract" => "Contract",
                "internship" => "Internship",
                "intern" => "Internship",
                _ => string.Empty
            };
        }

        private static string NormalizeWorkMode(string? value)
        {
            return value?.Trim().ToLowerInvariant() switch
            {
                "on-site" => "On-site",
                "onsite" => "On-site",
                "on site" => "On-site",
                "remote" => "Remote",
                "hybrid" => "Hybrid",
                _ => string.Empty
            };
        }

        private static string CreateBebityExternalJobId(string linkedInJobUrl)
        {
            Match idMatch = Regex.Match(
                linkedInJobUrl,
                @"(?:/jobs/view/(?:[^/?#]*-)?|[?&]currentJobId=)(\d{6,})",
                RegexOptions.IgnoreCase
            );

            if (!idMatch.Success)
            {
                idMatch = Regex.Match(
                    linkedInJobUrl,
                    @"\b(\d{8,})\b"
                );
            }

            if (idMatch.Success)
            {
                return $"linkedin_{idMatch.Groups[1].Value}";
            }

            // Fallback remains stable for the same LinkedIn URL and avoids
            // title/company heuristic deduplication.
            return "linkedin_url_" +
                CreateSha256Hash(linkedInJobUrl.Trim().ToLowerInvariant());
        }

        private static DateTime? GetBebityPostedDate(JsonElement item)
        {
            string postedTime = GetString(item, "postedTime").Trim();

            if (!string.IsNullOrWhiteSpace(postedTime))
            {
                Match relativeMatch = Regex.Match(
                    postedTime,
                    @"(?<count>\d+)\s*(?<unit>minute|hour|day|week|month)s?",
                    RegexOptions.IgnoreCase
                );

                if (relativeMatch.Success &&
                    int.TryParse(relativeMatch.Groups["count"].Value, out int count))
                {
                    DateTime now = DateTime.UtcNow;
                    string unit = relativeMatch.Groups["unit"].Value.ToLowerInvariant();

                    return unit switch
                    {
                        "minute" => now.AddMinutes(-count),
                        "hour" => now.AddHours(-count),
                        "day" => now.AddDays(-count),
                        "week" => now.AddDays(-(count * 7)),
                        "month" => now.AddMonths(-count),
                        _ => null
                    };
                }
            }

            string publishedAt = GetString(item, "publishedAt").Trim();

            if (DateTime.TryParse(publishedAt, out DateTime parsedDate))
            {
                return DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
            }

            return null;
        }

        private static string GetString(
            JsonElement element,
            string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement value) ||
                value.ValueKind == JsonValueKind.Null ||
                value.ValueKind == JsonValueKind.Undefined)
            {
                return string.Empty;
            }

            return value.ToString();
        }

        private static string NormalizeDuplicateField(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return string.Join(
                " ",
                value.Trim()
                    .ToLowerInvariant()
                    .Split(
                        (char[]?)null,
                        StringSplitOptions.RemoveEmptyEntries
                    )
            );
        }

        private static string CreateDescriptionHash(string? description)
        {
            return CreateSha256Hash(
                NormalizeForHash(description)
            );
        }

        private static string NormalizeForHash(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return string.Join(
                " ",
                value
                    .Trim()
                    .ToLowerInvariant()
                    .Split(
                        new[] { ' ', '\r', '\n', '\t' },
                        StringSplitOptions.RemoveEmptyEntries
                    )
            );
        }

        private static string CreateSha256Hash(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            byte[] hashBytes = SHA256.HashData(bytes);
            return Convert.ToHexString(hashBytes);
        }
    }
}
