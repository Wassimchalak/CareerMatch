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
    /// Handles JSearch, duplicate removal, job persistence,
    /// permanent OpenAI classification caching, immediate job returns,
    /// and the separate AI-matching flow.
    /// </summary>
    public class JobSearchService
    {
        // Allowed classification values stored by CareerMatch.
        private static readonly HashSet<string> AllowedEmploymentTypes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "Full-time",
                "Part-time",
                "Contract",
                "Internship"
            };

        private static readonly HashSet<string> AllowedWorkModes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "On-site",
                "Remote",
                "Hybrid"
            };

        // Sends HTTP requests to JSearch.
        private readonly HttpClient _httpClient;

        // Reads JSearch configuration values.
        private readonly IConfiguration _configuration;

        // Creates SQL Server connections for Dapper.
        private readonly DbConnectionFactory _dbConnectionFactory;

        // Classifies uncached jobs in one OpenAI request.
        private readonly AIService _aiService;

        // Calculates and caches candidate/job match results.
        private readonly MatchingService _matchingService;

        // Receives dependencies through dependency injection.
        public JobSearchService(
            HttpClient httpClient,
            IConfiguration configuration,
            DbConnectionFactory dbConnectionFactory,
            AIService aiService,
            MatchingService matchingService)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _dbConnectionFactory = dbConnectionFactory;
            _aiService = aiService;
            _matchingService = matchingService;
        }

        /// <summary>
        /// Searches JSearch using role, selected work preferences, and location.
        ///
        /// Jobs are saved before classification so every job has a local JobId.
        /// Existing classifications are reused permanently when the title and
        /// description have not changed. All uncached jobs are classified in
        /// one OpenAI request, then the requested filters are applied locally.
        /// </summary>
        public async Task<List<JobSearchResponse>> SearchJobsAsync(
            JobSearchRequest request)
        {
            // Get raw jobs from JSearch without trusting its work/type filters.
            List<Job> jobs =
                await SearchJSearchAsync(request);

            // Remove duplicate jobs before touching SQL Server or OpenAI.
            List<Job> uniqueJobs =
                RemoveDuplicateJobs(jobs);

            if (uniqueJobs.Count == 0)
            {
                return new List<JobSearchResponse>();
            }

            // Holds only jobs whose permanent cache is missing or stale.
            var jobsToClassify =
                new List<Job>();

            // Save/update every job and load any reusable classification.
            foreach (Job job in uniqueJobs)
            {
                bool requiresClassification =
                    await SaveOrUpdateJobAsync(job);

                if (requiresClassification)
                {
                    jobsToClassify.Add(job);
                }
            }

            // Classify every uncached job in ONE OpenAI request.
            if (jobsToClassify.Count > 0)
            {
                await ClassifyAndSaveJobsAsync(
                    jobsToClassify
                );
            }

            // Keep only jobs matching both user-selected filters.
            List<Job> filteredJobs = uniqueJobs
                .Where(job =>
                    FilterValuesMatch(
                        job.EmploymentType,
                        request.EmploymentType
                    ) &&
                    FilterValuesMatch(
                        job.WorkMode,
                        request.WorkType
                    )
                )
                .ToList();

            // Return jobs immediately without candidate/job matching.
            return filteredJobs
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
                    MatchStatus = "calculating..."
                })
                .ToList();
        }

        /// <summary>
        /// Loads selected jobs and calculates AI matches for the authenticated user.
        /// This existing matching flow is intentionally unchanged.
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
                        MatchScore =
                            found
                                ? match!.MatchScore
                                : null,
                        MatchExplanation =
                            found
                                ? match!.MatchExplanation
                                : "Match analysis was not returned.",
                        Recommendation =
                            found
                                ? match!.Recommendation
                                : "Try calculating the match again.",
                        MatchStatus =
                            found
                                ? "Completed"
                                : "Failed"
                    };
                })
                .OrderByDescending(job =>
                    job.MatchScore
                )
                .ToList();
        }

        /// <summary>
        /// Calls JSearch using role, employment type, work mode, and location keywords.
        /// Employment type and work mode are still classified later from title and description.
        /// </summary>
       private async Task<List<Job>> SearchJSearchAsync(
    JobSearchRequest request)
{
    var jobs = new List<Job>();

    string apiKey =
        _configuration["JSearch:ApiKey"]
        ?? string.Empty;

    string host =
        _configuration["JSearch:Host"]
        ?? "jsearch.p.rapidapi.com";

    if (string.IsNullOrWhiteSpace(apiKey))
    {
        Console.WriteLine(
            "JSEARCH ERROR: API key is missing."
        );

        return jobs;
    }

    try
    {
        // Ask OpenAI to convert the user's role into the language
        // required for job searches in the selected country.
        string translatedRole =
            await _aiService.TranslateRoleForCountryAsync(
                request.Role,
                request.Country
            );

        // Create a separate request so the original frontend request
        // remains unchanged while JSearch receives the translated role.
        var translatedRequest =
            new JobSearchRequest
            {
                Country = request.Country,
                City = request.City,
                Role = translatedRole,
                WorkType = request.WorkType,
                EmploymentType = request.EmploymentType
            };

        // Build a query containing the translated role, employment type,
        // work mode, city, and country when available.
        string query =
            BuildJSearchQuery(translatedRequest);

        Console.WriteLine(
            $"ROLE TRANSLATION: '{request.Role}' -> " +
            $"'{translatedRole}' for {request.Country}"
        );

        string countryCode =
            GetJSearchCountryCode(
                request.Country
            );

        // Version v2 uses /search, not /search-v2.
        string url =
            $"https://{host}/search" +
            $"?query={Uri.EscapeDataString(query)}" +
            "&page=1" +
            "&num_pages=1" +
            $"&country={Uri.EscapeDataString(countryCode)}" +
            "&date_posted=all";

        using var httpRequest =
            new HttpRequestMessage(
                HttpMethod.Get,
                url
            );

        httpRequest.Headers.Add(
            "x-rapidapi-key",
            apiKey
        );

        httpRequest.Headers.Add(
            "x-rapidapi-host",
            host
        );

        httpRequest.Headers.Accept.Add(
            new System.Net.Http.Headers
                .MediaTypeWithQualityHeaderValue(
                    "application/json"
                )
        );

        Console.WriteLine(
            $"JSEARCH REQUEST URL: {url}"
        );

        using HttpResponseMessage response =
            await _httpClient.SendAsync(
                httpRequest
            );

        string jsonResponse =
            await response.Content
                .ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine(
                $"JSEARCH ERROR: " +
                $"{(int)response.StatusCode} " +
                $"{response.StatusCode} - " +
                $"{jsonResponse}"
            );

            return jobs;
        }

        using JsonDocument json =
            JsonDocument.Parse(
                jsonResponse
            );

        // The v2 response normally has:
        //
        // {
        //     "status": "OK",
        //     "data": [
        //         { job_id, job_title, ... }
        //     ]
        // }
        //
        // Therefore, data itself is the jobs array.
        if (!json.RootElement.TryGetProperty(
                "data",
                out JsonElement dataElement
            ))
        {
            Console.WriteLine(
                "JSEARCH ERROR: Response does not contain a data property."
            );

            return jobs;
        }

        JsonElement jobsArray;

        // Normal v2 response:
        // "data": [...]
        if (dataElement.ValueKind ==
            JsonValueKind.Array)
        {
            jobsArray = dataElement;
        }
        // Defensive support in case the provider returns:
        // "data": { "jobs": [...] }
        else if (
            dataElement.ValueKind ==
                JsonValueKind.Object
            &&
            dataElement.TryGetProperty(
                "jobs",
                out JsonElement nestedJobs
            )
            &&
            nestedJobs.ValueKind ==
                JsonValueKind.Array
        )
        {
            jobsArray = nestedJobs;
        }
        else
        {
            Console.WriteLine(
                "JSEARCH ERROR: The data property is not a jobs array."
            );

            return jobs;
        }

        foreach (
            JsonElement item
            in jobsArray.EnumerateArray()
        )
        {
            string externalId =
                GetString(
                    item,
                    "job_id"
                );

            if (string.IsNullOrWhiteSpace(
                externalId
            ))
            {
                continue;
            }

            string title =
                GetString(
                    item,
                    "job_title"
                );

            if (string.IsNullOrWhiteSpace(
                title
            ))
            {
                continue;
            }

            string description =
                GetString(
                    item,
                    "job_description"
                );

            string country =
                GetString(
                    item,
                    "job_country"
                );

            string city =
                GetString(
                    item,
                    "job_city"
                );

            string jobUrl =
                GetString(
                    item,
                    "job_apply_link"
                );

            // Some results may not have job_apply_link.
            // Use the Google Jobs link as a fallback.
            if (string.IsNullOrWhiteSpace(
                jobUrl
            ))
            {
                jobUrl =
                    GetString(
                        item,
                        "job_google_link"
                    );
            }

            DateTime? postedDate = null;

            string postedDateText =
                GetString(
                    item,
                    "job_posted_at_datetime_utc"
                );

            if (DateTime.TryParse(
                    postedDateText,
                    out DateTime parsedDate
                ))
            {
                postedDate =
                    parsedDate.ToUniversalTime();
            }

            jobs.Add(
                new Job
                {
                    ExternalJobId =
                        "jsearch_" +
                        externalId,

                    Title =
                        title,

                    CompanyName =
                        GetString(
                            item,
                            "employer_name"
                        ),

                    Country =
                        string.IsNullOrWhiteSpace(
                            country
                        )
                            ? request.Country
                            : country,

                    City =
                        string.IsNullOrWhiteSpace(
                            city
                        )
                            ? request.City
                            : city,

                    Description =
                        description,

                    DescriptionHash =
                        CreateDescriptionHash(
                            description
                        ),

                    ClassificationHash =
                        CreateClassificationHash(
                            title,
                            description
                        ),

                    JobUrl =
                        jobUrl,

                    // These values will be loaded from the SQL cache
                    // or generated later by OpenAI.
                    EmploymentType =
                        null,

                    WorkMode =
                        null,

                    ClassifiedAt =
                        null,

                    PostedDate =
                        postedDate,

                    CreatedAt =
                        DateTime.UtcNow
                }
            );
        }

        Console.WriteLine(
            $"JSEARCH RETURNED: {jobs.Count} valid jobs"
        );
    }
    catch (TaskCanceledException exception)
    {
        Console.WriteLine(
            $"JSEARCH TIMEOUT: " +
            $"{exception.Message}"
        );
    }
    catch (JsonException exception)
    {
        Console.WriteLine(
            $"JSEARCH JSON ERROR: " +
            $"{exception.Message}"
        );
    }
    catch (Exception exception)
    {
        Console.WriteLine(
            $"JSEARCH ERROR: " +
            $"{exception.Message}"
        );
    }

    return jobs;
}

        /// <summary>
        /// Sends all uncached jobs in one OpenAI request and permanently saves
        /// valid classifications against the exact ClassificationHash.
        /// </summary>
        private async Task ClassifyAndSaveJobsAsync(
            List<Job> jobsToClassify)
        {
            List<AIJobClassificationItem> aiResults =
                await _aiService.ClassifyJobsAsync(
                    jobsToClassify
                );

            Dictionary<int, AIJobClassificationItem> resultByJobId =
                aiResults
                    .GroupBy(result =>
                        result.JobId
                    )
                    .ToDictionary(
                        group => group.Key,
                        group => group.First()
                    );

            var updates =
                new List<object>();

            foreach (Job job in jobsToClassify)
            {
                // Missing or invalid AI values use the required safe defaults.
                resultByJobId.TryGetValue(
                    job.JobId,
                    out AIJobClassificationItem? result
                );

                string employmentType =
                    GetValidEmploymentType(
                        result?.EmploymentType
                    );

                string workMode =
                    GetValidWorkMode(
                        result?.WorkMode
                    );

                DateTime classifiedAt =
                    DateTime.UtcNow;

                job.EmploymentType = employmentType;
                job.WorkMode = workMode;
                job.ClassifiedAt = classifiedAt;

                updates.Add(
                    new
                    {
                        job.JobId,
                        job.ClassificationHash,
                        EmploymentType = employmentType,
                        WorkMode = workMode,
                        ClassifiedAt = classifiedAt
                    }
                );
            }

            using var connection =
                _dbConnectionFactory.CreateConnection();

            // The hash predicate prevents an older result from overwriting
            // a newer title/description classification during concurrent searches.
            await connection.ExecuteAsync(
                @"
                UPDATE Jobs
                SET
                    EmploymentType = @EmploymentType,
                    WorkMode = @WorkMode,
                    ClassifiedAt = @ClassifiedAt
                WHERE JobId = @JobId
                  AND ClassificationHash = @ClassificationHash;
                ",
                updates
            );
        }

        /// <summary>
        /// Inserts or updates one job and determines whether OpenAI classification
        /// is required. A valid unchanged cache is copied back into the Job object.
        /// </summary>
        private async Task<bool> SaveOrUpdateJobAsync(
            Job job)
        {
            using var connection =
                _dbConnectionFactory.CreateConnection();

            Job? existingJob =
                await connection
                    .QueryFirstOrDefaultAsync<Job>(
                        @"
                        SELECT
                            JobId,
                            ClassificationHash,
                            EmploymentType,
                            WorkMode,
                            ClassifiedAt
                        FROM Jobs
                        WHERE ExternalJobId = @ExternalJobId;
                        ",
                        new
                        {
                            job.ExternalJobId
                        }
                    );

            if (existingJob == null)
            {
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
                            ClassificationHash,
                            ClassifiedAt,
                            JobUrl,
                            EmploymentType,
                            WorkMode,
                            PostedDate,
                            CreatedAt
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
                            @ClassificationHash,
                            NULL,
                            @JobUrl,
                            NULL,
                            NULL,
                            @PostedDate,
                            @CreatedAt
                        );
                        ",
                        job
                    );

                return true;
            }

            job.JobId =
                existingJob.JobId;

            bool classificationHashMatches =
                !string.IsNullOrWhiteSpace(
                    existingJob.ClassificationHash
                ) &&
                string.Equals(
                    existingJob.ClassificationHash,
                    job.ClassificationHash,
                    StringComparison.Ordinal
                );

            bool cacheIsComplete =
                classificationHashMatches &&
                IsValidEmploymentType(
                    existingJob.EmploymentType
                ) &&
                IsValidWorkMode(
                    existingJob.WorkMode
                );

            if (cacheIsComplete)
            {
                // Preserve and reuse the permanent classification cache.
                job.EmploymentType =
                    GetValidEmploymentType(
                        existingJob.EmploymentType
                    );

                job.WorkMode =
                    GetValidWorkMode(
                        existingJob.WorkMode
                    );

                job.ClassifiedAt =
                    existingJob.ClassifiedAt;

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
                        ClassificationHash = @ClassificationHash,
                        JobUrl = @JobUrl,
                        PostedDate = @PostedDate
                    WHERE JobId = @JobId;
                    ",
                    job
                );

                return false;
            }

            // The title/description changed or the cache is incomplete.
            // Clear stale values before adding the job to the batch request.
            job.EmploymentType = null;
            job.WorkMode = null;
            job.ClassifiedAt = null;

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
                    ClassificationHash = @ClassificationHash,
                    ClassifiedAt = NULL,
                    JobUrl = @JobUrl,
                    EmploymentType = NULL,
                    WorkMode = NULL,
                    PostedDate = @PostedDate
                WHERE JobId = @JobId;
                ",
                job
            );

            return true;
        }


        /// <summary>
        /// Builds a JSearch query using the selected role, employment type,
        /// work mode, and optional city.
        ///
        /// These keywords improve retrieval only.
        /// OpenAI still classifies every uncached job using title and description.
        /// </summary>
        private static string BuildJSearchQuery(
            JobSearchRequest request)
        {
            var queryParts =
                new List<string>();

            // Add the role selected by the user.
            if (!string.IsNullOrWhiteSpace(
                    request.Role
                ))
            {
                queryParts.Add(
                    request.Role.Trim()
                );
            }

            // Add the employment type as a search keyword.
            string employmentTypeKeyword =
                GetEmploymentTypeSearchKeyword(
                    request.EmploymentType
                );

            if (!string.IsNullOrWhiteSpace(
                    employmentTypeKeyword
                ))
            {
                queryParts.Add(
                    employmentTypeKeyword
                );
            }

            // Add the work mode as a search keyword.
            string workModeKeyword =
                GetWorkModeSearchKeyword(
                    request.WorkType
                );

            if (!string.IsNullOrWhiteSpace(
                    workModeKeyword
                ))
            {
                queryParts.Add(
                    workModeKeyword
                );
            }

            // Add the city only when the user selected one.
            if (!string.IsNullOrWhiteSpace(
                    request.City
                ))
            {
                queryParts.Add(
                    $"in {request.City.Trim()}"
                );
            }

            return string.Join(
                " ",
                queryParts
            );
        }

        /// <summary>
        /// Converts the selected employment type into a useful JSearch keyword.
        /// This method improves retrieval and does not classify jobs.
        /// </summary>
        private static string GetEmploymentTypeSearchKeyword(
            string? employmentType)
        {
            string normalizedValue =
                NormalizeComparableValue(
                    employmentType
                );

            return normalizedValue switch
            {
                "intern" => "internship",
                "internship" => "internship",

                "fulltime" => "full time",

                "parttime" => "part time",

                "contract" => "contract",
                "contractor" => "contract",
                "freelance" => "contract",
                "temporary" => "contract",

                _ => string.Empty
            };
        }

        /// <summary>
        /// Converts the selected work mode into a useful JSearch keyword.
        /// This method improves retrieval and does not classify jobs.
        /// </summary>
        private static string GetWorkModeSearchKeyword(
            string? workMode)
        {
            string normalizedValue =
                NormalizeComparableValue(
                    workMode
                );

            return normalizedValue switch
            {
                "remote" => "remote",
                "fullyremote" => "remote",
                "workfromhome" => "remote",
                "wfh" => "remote",

                "hybrid" => "hybrid",

                "onsite" => "on site",

                _ => string.Empty
            };
        }

        /// <summary>
        /// Removes duplicates first by ExternalJobId,
        /// then by normalized title, company, city, and country.
        /// </summary>
        private static List<Job> RemoveDuplicateJobs(
            IEnumerable<Job> jobs)
        {
            IEnumerable<Job> externalIdUniqueJobs =
                jobs
                    .Where(job =>
                        !string.IsNullOrWhiteSpace(
                            job.ExternalJobId
                        )
                    )
                    .GroupBy(
                        job =>
                            job.ExternalJobId.Trim(),
                        StringComparer.OrdinalIgnoreCase
                    )
                    .Select(group =>
                        group.First()
                    );

            return externalIdUniqueJobs
                .GroupBy(job =>
                    CreateDuplicateKey(job)
                )
                .Select(group =>
                    group.First()
                )
                .ToList();
        }

        /// <summary>
        /// Creates a stable duplicate key.
        /// </summary>
        private static string CreateDuplicateKey(
            Job job)
        {
            return string.Join(
                "|",
                NormalizeDuplicateValue(
                    job.Title
                ),
                NormalizeDuplicateValue(
                    job.CompanyName
                ),
                NormalizeDuplicateValue(
                    job.City
                ),
                NormalizeDuplicateValue(
                    job.Country
                )
            );
        }

        /// <summary>
        /// Normalizes duplicate-comparison values.
        /// </summary>
        private static string NormalizeDuplicateValue(
            string? value)
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
                        ' ',
                        StringSplitOptions
                            .RemoveEmptyEntries
                    )
            );
        }

        /// <summary>
        /// Compares a stored classification to a frontend filter without
        /// performing any title-based detection or guessing.
        /// </summary>
        private static bool FilterValuesMatch(
            string? classifiedValue,
            string? requestedValue)
        {
            if (string.IsNullOrWhiteSpace(requestedValue))
            {
                return true;
            }

            return string.Equals(
                NormalizeComparableValue(
                    classifiedValue
                ),
                NormalizeComparableValue(
                    requestedValue
                ),
                StringComparison.Ordinal
            );
        }

        /// <summary>
        /// Removes punctuation, spaces, hyphens, and casing for comparison only.
        /// </summary>
        private static string NormalizeComparableValue(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return Regex.Replace(
                value.Trim()
                    .ToLowerInvariant(),
                @"[^a-z0-9]",
                string.Empty
            );
        }

        private static bool IsValidEmploymentType(
            string? value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   AllowedEmploymentTypes.Contains(value);
        }

        private static bool IsValidWorkMode(
            string? value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   AllowedWorkModes.Contains(value);
        }

        private static string GetValidEmploymentType(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Full-time";
            }

            return AllowedEmploymentTypes
                .FirstOrDefault(allowed =>
                    string.Equals(
                        allowed,
                        value.Trim(),
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                ?? "Full-time";
        }

        private static string GetValidWorkMode(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "On-site";
            }

            return AllowedWorkModes
                .FirstOrDefault(allowed =>
                    string.Equals(
                        allowed,
                        value.Trim(),
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                ?? "On-site";
        }

        /// <summary>
        /// Safely reads a JSON property.
        /// </summary>
        private static string GetString(
            JsonElement element,
            string propertyName)
        {
            if (!element.TryGetProperty(
                    propertyName,
                    out JsonElement value
                ))
            {
                return string.Empty;
            }

            if (value.ValueKind ==
                    JsonValueKind.Null ||
                value.ValueKind ==
                    JsonValueKind.Undefined)
            {
                return string.Empty;
            }

            return value.ToString();
        }

        /// <summary>
        /// Converts country names and aliases into JSearch country codes.
        /// </summary>
        private static string GetJSearchCountryCode(
            string country)
        {
            if (string.IsNullOrWhiteSpace(country))
            {
                return "us";
            }

           return country
    .Trim()
    .ToLowerInvariant() switch
{
    // Middle East
    "united arab emirates" => "ae",
    "uae" => "ae",
    "saudi arabia" => "sa",
    "ksa" => "sa",
    "qatar" => "qa",
    "kuwait" => "kw",
    "oman" => "om",
    "bahrain" => "bh",
    "lebanon" => "lb",
    "jordan" => "jo",
    "iraq" => "iq",

    // Africa
    "egypt" => "eg",
    "morocco" => "ma",
    "tunisia" => "tn",
    // North America
    "united states" => "us",
    "canada" => "ca",
    "mexico" => "mx",

    // South America
    "brazil" => "br",

    // Europe
    "united kingdom" => "gb",
    "france" => "fr",
    "italy" => "it",
    "spain" => "es",

    // Asia
    "india" => "in",
    "japan" => "jp",




    _ => "us"
};
        }

        /// <summary>
        /// Creates a normalized SHA-256 hash of the description.
        /// Existing candidate/job matching cache behavior remains unchanged.
        /// </summary>
        private static string CreateDescriptionHash(
            string? description)
        {
            return CreateSha256Hash(
                NormalizeForHash(
                    description
                )
            );
        }

        /// <summary>
        /// Creates the permanent classification cache key from BOTH
        /// normalized title and normalized description.
        /// </summary>
        private static string CreateClassificationHash(
            string? title,
            string? description)
        {
            string normalizedInput =
                NormalizeForHash(title) +
                "\n" +
                NormalizeForHash(description);

            return CreateSha256Hash(
                normalizedInput
            );
        }

        /// <summary>
        /// Normalizes text before hashing so harmless whitespace differences
        /// do not trigger unnecessary OpenAI classification requests.
        /// </summary>
        private static string NormalizeForHash(
            string? value)
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
                        new[]
                        {
                            ' ',
                            '\r',
                            '\n',
                            '\t'
                        },
                        StringSplitOptions.RemoveEmptyEntries
                    )
            );
        }

        private static string CreateSha256Hash(
            string value)
        {
            byte[] bytes =
                Encoding.UTF8.GetBytes(
                    value
                );

            byte[] hashBytes =
                SHA256.HashData(
                    bytes
                );

            return Convert.ToHexString(
                hashBytes
            );
        }
    }
}