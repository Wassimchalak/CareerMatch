using CareerMatch.API.DTOs;
using CareerMatch.API.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CareerMatch.API.Services
{
    /// <summary>
    /// Handles all communication with OpenAI.
    /// Main responsibilities:
    /// - Extract the candidate's primary role and skills from a CV.
    /// - Extract the primary role and required skills from a job description.
    /// - Match one candidate against multiple jobs in one OpenAI request.
    /// - Rewrite a CV for a specific job.
    /// - Generate a personalized cover letter.
    /// - Generate expected interview questions
    /// - classifies jobs employment tyoe and workmMode
    /// </summary>
    public class AIService
    {
        // Maximum number of job-description characters sent during matching.
        // Keeping this small reduces prompt size, token usage, and response time.
        private const int MatchDescriptionLimit = 1800;

        // A slightly larger limit is used for CV rewriting and cover letters,
        // where more job context helps produce better writing.
        private const int DocumentJobDescriptionLimit = 3000;

        // Used to send HTTP requests to the OpenAI API.
        private readonly HttpClient _httpClient;

        // Used to read the API key and model from appsettings.json.
        private readonly IConfiguration _configuration;

        // Shared JSON settings used when reading model responses.
        private static readonly JsonSerializerOptions JsonOptions =
            new()
            {
                PropertyNameCaseInsensitive = true
            };

        /// <summary>
        /// Receives required dependencies through ASP.NET Core dependency injection.
        /// </summary>
        public AIService(
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        /// <summary>
        /// Extracts the candidate's main role and normalized skills from a CV.
        /// </summary>
        public async Task<AICVAnalysisResult> ExtractSkillsAsync(
            string cvText)
        {
            // Return an empty result if no readable CV text exists.
            if (string.IsNullOrWhiteSpace(cvText))
                return new AICVAnalysisResult();

            // Remove unnecessary whitespace before sending the CV to OpenAI.
            string cleanedCVText = CleanText(cvText);

            // Keep the prompt short and request only the exact JSON structure needed.
string prompt = $@"
Analyze the document in any language and extract the candidate's main professional role and real skills only if the document is a valid CV or resume.

The document may be written in English, Arabic, French, Spanish, German, or any other language.

LANGUAGE RULES:
- Detect the document language automatically.
- Fully understand and analyze the CV even if it is written entirely or partially in Arabic, French, or another non-English language.
- Never reject a valid CV because of its language.
- Translate the extracted primary role into clear, normalized English.
- Translate general professional skill names into their common English names.
- Keep internationally recognized technology names unchanged, such as C#, Java, React, SQL Server, Python, AutoCAD, SAP, Excel, or Power BI.
- The JSON property names and output structure must always remain in English.

PRIMARY ROLE RULES:
- You must determine the candidate's most likely primary professional role for every valid CV.
- First, look for an explicitly written role, job title, profession, career objective, professional summary, or current position.
- If the primary role is written in Arabic, French, or another language, translate and normalize it into English.
- If no primary role is explicitly written, infer the most likely role from the candidate's work experience, education, projects, responsibilities, qualifications, and strongest skills.
- Choose the single role that best represents the candidate's overall professional profile.
- Prefer the role supported by the candidate's most recent, longest, or most relevant experience.
- Do not return an empty primaryRole for a valid CV only because the role was not directly written.
- Do not invent a role without supporting evidence in the CV.
- Ignore seniority words such as Junior, Senior, Lead, Principal, Entry-Level, or Expert.
- Normalize similar job titles into a common English role.
- Examples:
  - ""مطور خلفيات"" becomes ""Backend Developer"".
  - ""مهندس برمجيات"" becomes ""Software Engineer"".
  - ""محاسب"" becomes ""Accountant"".
  - ""معلمة اقتصاد"" becomes ""Economics Teacher"".
  - ""Développeur web"" becomes ""Web Developer"".
  - ""Ingénieur logiciel"" becomes ""Software Engineer"".
  - ""Comptable"" becomes ""Accountant"".

Return JSON only in this exact format:
{{
  ""primaryRole"": ""Backend Developer"",
  ""skills"": [
    {{
      ""skillName"": ""C#"",
      ""yearsOfExperience"": 2
    }}
  ]
}}

INVALID DOCUMENT RULE:
- If the document is not a CV or resume, return exactly:
{{
  ""primaryRole"": """",
  ""skills"": []
}}
- Also return the empty result if the document is blank, unreadable, contains unrelated content, or does not include meaningful candidate information such as experience, education, projects, qualifications, responsibilities, or skills.
- Do not treat lessons, interview questions, invoices, articles, books, assignments, reports, certificates alone, job descriptions, or random text as a CV.
- Do not guess a role or skills from unrelated content.

VALID CV RULES:
- Extract the candidate's most likely main professional role.
- Return the primary role in normalized English.
- Use common normalized English skill names.
- Keep different technologies separate.
- Extract only real technical, professional, administrative, educational, medical, financial, creative, or business skills supported by the CV.
- Estimate years of experience only when supported by dates, durations, repeated usage, or clear experience context; otherwise use 0.
- Correctly understand dates, job titles, responsibilities, education, qualifications, and experience written in Arabic, French, or other languages.
- Use the candidate's responsibilities and experience to infer the role when the title is missing.
- Do not translate company names, university names, product names, or certification names unless necessary for understanding.
- Do not invent skills, experience, roles, employers, education, qualifications, dates, or responsibilities.
- Do not include languages spoken as professional skills unless they are clearly relevant to the candidate's work.
- Do not include vague traits such as hardworking, motivated, or punctual unless they are clearly presented as professional competencies.
- No markdown, commentary, or explanation.
- Return valid JSON only.

DOCUMENT:
{cleanedCVText}";



            // Send the prompt using the single configured model.
            string outputText =
                await SendPromptToOpenAIAsync(prompt);
            // Convert the returned JSON into the expected DTO.
            return JsonSerializer.Deserialize<AICVAnalysisResult>(
                       outputText,
                       JsonOptions
                   )
                   ?? new AICVAnalysisResult();
        }

        /// <summary>
        /// Extracts a job's main role and required skills.
        ///
        /// This method remains available for CV refinement and cover-letter features,
        /// even though live job matching no longer depends on stored job skills.
        /// </summary>
        public async Task<AIJobAnalysisResult>
            ExtractRequiredSkillsAsync(
                string jobDescription)
        {
            // Return an empty result when the description is missing.
            if (string.IsNullOrWhiteSpace(jobDescription))
                return new AIJobAnalysisResult();

            // Shorten the description to reduce token usage.
            string preparedDescription =
                PrepareJobDescription(
                    jobDescription,
                    DocumentJobDescriptionLimit
                );

            string prompt = $@"
Extract the job's main role and required skills.

Return JSON only:
{{
  ""primaryRole"": ""Backend Developer"",
  ""skills"": [
    {{
      ""skillName"": ""C#"",
      ""requiredYears"": 3,
      ""importance"": ""Required""
    }}
  ]
}}

Rules:
- Ignore seniority in primaryRole.
- Extract real technical or professional skills only.
- Normalize skill names.
- requiredYears: use the stated value; otherwise use 0.
- importance: Required, Preferred, or Nice-to-have.
- Do not invent information.
- No markdown or explanation.

JOB:
{preparedDescription}";

            string outputText =
                await SendPromptToOpenAIAsync(prompt);
                 
            return JsonSerializer.Deserialize<AIJobAnalysisResult>(
                       outputText,
                       JsonOptions
                   )
                   ?? new AIJobAnalysisResult();
        }

        /// <summary>
        /// Matches one candidate against multiple jobs in one OpenAI request.
        ///
        /// The method sends:
        /// - Candidate primary role.
        /// - Candidate extracted skills and years of experience.
        /// - User search preferences.
        /// - The unique jobs that still require matching.
        ///
        /// It does not send the full CV text.
        /// </summary>
        public async Task<List<AIMatchResult>>
            GenerateJobMatchesAsync(
                string cvPrimaryRole,
                IReadOnlyCollection<AIExtractedSkill> cvSkills,
                JobSearchRequest preferences,
                IReadOnlyCollection<Job> jobs)
        {
            // No jobs means there is nothing to send to OpenAI.
            if (jobs.Count == 0)
                return new List<AIMatchResult>();

            // Build a small structured candidate profile.
            var candidate = new
            {
                role = cvPrimaryRole,

                skills = cvSkills.Select(skill => new
                {
                    name = skill.SkillName,
                    years = skill.YearsOfExperience
                })
            };

            // Send only fields that affect matching.
            // Do not send company name, job URL, external id, or database hashes.
            var jobInputs = jobs.Select(job => new
            {
                id = job.JobId,
                title = job.Title,
                country = job.Country,
                city = job.City,
                mode = job.WorkMode,
                type = job.EmploymentType,

                // Preserve only the most useful parts of long descriptions.
                description = PrepareJobDescription(
                    job.Description,
                    MatchDescriptionLimit
                )
            });

            // Include the preferences selected by the user.
            var searchPreferences = new
            {
                country = preferences.Country,
                city = preferences.City,
                role = preferences.Role,
                mode = preferences.WorkType,
                type = preferences.EmploymentType
            };

            // Convert all structured data to compact JSON.
            string candidateJson =
                JsonSerializer.Serialize(candidate);

            string preferencesJson =
                JsonSerializer.Serialize(searchPreferences);

            string jobsJson =
                JsonSerializer.Serialize(jobInputs);

            // Ask for one short result per supplied job id.
          string prompt = $@"
Match one candidate against every job independently and fairly.

Return JSON only in this exact format:
{{
  ""matches"": [
    {{
      ""jobId"": 123,
      ""matchScore"": 85,
      ""matchExplanation"": ""Strong .NET and SQL alignment; Azure experience is not shown."",
      ""recommendation"": ""Highlight backend projects and strengthen Azure knowledge.""
    }}
  ]
}}

SCORING PRINCIPLES:
- Evaluate the candidate using confirmed skills, role alignment, transferable skills, seniority, location, work mode, and employment type.
- Score each job independently.
- A listed candidate skill is confirmed even when yearsOfExperience is 0.
- yearsOfExperience = 0 means the duration is unknown, not that the candidate has no experience.
- Never treat a confirmed skill with unknown duration as a missing skill.
- Do not give a score of 0 only because years of experience are not stated.
- Only give a very low score when there is almost no meaningful role or skill alignment.
- Missing one technology must not destroy the entire score when the candidate has strong related skills.
- Recognize transferable technologies and concepts where reasonable.
- Do not invent skills, experience, projects, or qualifications.
- Do not assume experience that is not supplied.

EXPERIENCE RULES:
- If the job does not specify required years, do not penalize the candidate for unknown years.
- If the job specifies years and the candidate skill has yearsOfExperience = 0, apply only a small uncertainty penalty.
- If the candidate has fewer confirmed years than required, apply a proportional penalty rather than treating the skill as missing.
- Seniority mismatch should reduce the score, but should not automatically make it 0.
- Internship and portfolio skills may support junior or entry-level roles even when formal years are unknown.

SKILL RULES:
- Exact required skill present: strong positive contribution.
- Closely related or transferable skill: partial positive contribution.
- Required skill absent: penalty based on importance.
- Preferred or nice-to-have skill absent: small or no penalty.
- Do not penalize for technologies that are merely examples or alternatives.
- When a job says one of several technologies is acceptable, matching any one of them is sufficient.
- General skills such as REST APIs, SQL, Git, OOP, testing, databases, and Agile should count across related roles.

ROLE RULES:
- Strong title and skill alignment should score well even when the titles are not identical.
- Backend Developer, Software Engineer, .NET Developer, Java Developer, and Full Stack Developer may partially overlap depending on the supplied skills.
- Penalize only when the job's core function clearly differs from the candidate's profile.

LOCATION AND WORK RULES:
- Do not penalize location for remote jobs.
- Apply only a small penalty for city mismatch when the country matches.
- Apply a moderate penalty for country mismatch only when the role is not remote.
- Employment type and work mode should influence the score less than core skills and role fit.

SCORE GUIDE:
- 90-100: Excellent fit; most core requirements are clearly met.
- 75-89: Strong fit; good core alignment with a few gaps.
- 60-74: Moderate fit; several relevant skills but meaningful gaps.
- 40-59: Weak fit; some transferable alignment but major missing requirements.
- 20-39: Poor fit; limited relevant alignment.
- 0-19: Almost no meaningful alignment.

OUTPUT RULES:
- Return exactly one result for every supplied job id.
- Copy each supplied id into jobId unchanged.
- matchScore must be an integer from 0 to 100.
- matchExplanation must explain the strongest alignment and the main gap.
- matchExplanation: maximum 22 words.
- recommendation must be practical and based on the main missing requirement.
- recommendation: maximum 16 words.
- Never say the candidate has no experience when a skill is listed with yearsOfExperience = 0.
- Never return markdown, analysis, notes, or extra text.
- Return the JSON immediately.

CANDIDATE:
{candidateJson}

PREFERENCES:
{preferencesJson}

JOBS:
{jobsJson}";
            // Send one OpenAI request for all jobs.
            string outputText =
                await SendPromptToOpenAIAsync(prompt);

            // Deserialize the wrapper object containing the matches array.
            var response =
                JsonSerializer.Deserialize<AIJobMatchesResponse>(
                    outputText,
                    JsonOptions
                )
                ?? new AIJobMatchesResponse();

            // Build a set of real job ids so unexpected ids are ignored.
            var validJobIds = jobs
                .Select(job => job.JobId)
                .ToHashSet();

            // Keep only valid, unique results.
            return response.Matches
                .Where(match =>
                    validJobIds.Contains(match.JobId))
                .GroupBy(match => match.JobId)
                .Select(group => group.First())
                .ToList();
        }

        /// <summary>
        /// Rewrites a CV for a specific job while preserving factual accuracy.
        /// </summary>
        public async Task<string> RefineCVForJobAsync(
            string originalCVText,
            string cvSkillsText,
            string jobTitle,
            string companyName,
            string jobDescription
           )
        {
            if (string.IsNullOrWhiteSpace(originalCVText))
            {
                throw new ArgumentException(
                    "Original CV text cannot be empty."
                );
            }

            // Reduce the job-description size before sending it.
            string preparedJobDescription =
                PrepareJobDescription(
                    jobDescription,
                    DocumentJobDescriptionLimit
                );

            string prompt = $@"
Rewrite this CV for the job while preserving complete factual accuracy.

FIRST:
- Write the ENTIRE rewritten CV in English only.
- Do not detect or use the language of the job description.
- Even if the job description is written in another language, write the complete CV in natural, professional English.
- Keep technical terms such as .NET, C#, SQL, React, Azure, AWS, Java, JavaScript, Python, Docker, Kubernetes, Git, REST APIs, and similar technologies in their original form.

Rules:
- Do not add or remove languages,skills, experience, projects, education, certifications, dates, employers, achievements, or numbers.
- Do not invent information.
- Improve wording, grammar, structure, relevance, and ATS readability.
- Emphasize only existing qualifications that are relevant to the job.
- Use a clean single-column resume structure with clear headings and concise bullet points.
- Keep professional terminology appropriate for the detected language.
- No tables, icons, markdown fences, commentary, or placeholders.
- Return only the complete rewritten CV.

JOB:
{jobTitle} at {companyName}

JOB DESCRIPTION:
{preparedJobDescription}

CANDIDATE SKILLS:
{cvSkillsText}

ORIGINAL CV:
{CleanText(originalCVText)}";

            return await SendPromptToOpenAIAsync(prompt);
        }

        /// <summary>
        /// Generates a personalized and truthful cover letter.
        /// </summary>
        public async Task<string> GenerateCoverLetterAsync(
            string candidateCVText,
            string candidateSkillsText,
            string jobTitle,
            string companyName,
            string jobDescription
            )
        {
            if (string.IsNullOrWhiteSpace(candidateCVText))
            {
                throw new ArgumentException(
                    "Candidate CV text cannot be empty."
                );
            }

            // Reduce the job-description size before sending it.
            string preparedJobDescription =
                PrepareJobDescription(
                    jobDescription,
                    DocumentJobDescriptionLimit
                );

            string prompt = $@"
Write a truthful, personalized cover letter.

FIRST:
- Write the ENTIRE cover letter in English only.
- Do not detect or use the language of the job description.
- Even if the job description is written in another language, write the complete cover letter in natural, professional English.
- Keep technical terms such as .NET, C#, SQL, React, Azure, AWS, Java, JavaScript, Python, Docker, Kubernetes, Git, REST APIs, and similar technologies in their original form.

Rules:
- Use only facts supported by the CV and candidate skills.
- Do not invent, exaggerate, or assume any experience, skills, or achievements.
- Mention the exact job title and company name.
- Connect the candidate's strongest real qualifications to the job requirements.
- Use a professional, confident, and natural tone appropriate for the detected language.
- Keep the letter between 220 and 300 words.
- Do not use bullet points, markdown, placeholders, or commentary.
- Return only the complete cover letter.

JOB:
{jobTitle} at {companyName}

JOB DESCRIPTION:
{preparedJobDescription}

CANDIDATE SKILLS:
{candidateSkillsText}

CV:
{CleanText(candidateCVText)}";
            return await SendPromptToOpenAIAsync(prompt);
        }

        /// <summary>
        /// Sends a prompt to the OpenAI Responses API.
        ///
        /// This version uses one model only:
        /// OpenAI:Model from appsettings.json.
        /// </summary>
        // Add this public method inside AIService, before SendPromptToOpenAIAsync.

        /// <summary>
        /// Generates exactly five theoretical and five practical interview questions.
        /// </summary>
       public async Task<AIInterviewQuestionsResult>
    GenerateInterviewQuestionsAsync(
        string jobTitle,
        string companyName,
        string jobDescription)
{
    // Reject an empty job description because it is the main generation source.
    if (string.IsNullOrWhiteSpace(jobDescription))
    {
        throw new ArgumentException(
            "Job description cannot be empty."
        );
    }

    // Shortens the description using the existing document limit.
    string preparedJobDescription =
        PrepareJobDescription(
            jobDescription,
            DocumentJobDescriptionLimit
        );

    // Defines the exact JSON contract expected by AIInterviewQuestionsResult.
    string prompt = $@"
Generate interview preparation for this exact job.

LANGUAGE INSTRUCTIONS:
- Write every question,howToAnswer in English only.
- Do not detect or use the language of the job description.
- Even if the job description is written in another language, translate the relevant meaning and generate the complete output in English.
- Keep technical terms such as programming languages, frameworks, databases, cloud services, tools, libraries, APIs, protocols, and product names in their commonly used original form.
- Keep all JSON property names exactly as shown below in English.

Return JSON only in this exact format:
{{
  ""theoreticalQuestions"": [
    {{
      ""questionNumber"": 1,
      ""question"": ""Question written in the detected job-description language."",
      ""howToAnswer"": ""Answering guidance written in the detected job-description language.""
    }}
  ],
  ""practicalQuestions"": [
    {{
      ""questionNumber"": 6,
      ""question"": ""Practical question written in the detected job-description language."",
      ""howToAnswer"": ""Answering guidance written in the detected job-description language.""
    }}
  ]
}}

REQUIREMENTS:
- howToAnswer must explain exactly how the applicant should answer during the interview.
- Explain what the interviewer is evaluating.
- Describe the ideal structure of the response.
- Mention the important technical or business concepts that should be included.
- Mention common mistakes or weak answers to avoid when appropriate.
- For practical questions, explain the expected approach and reasoning instead of giving a complete solution.
- Teach the applicant how to think rather than providing a script to memorize.
- Keep each howToAnswer under 180 words.
- Preserve valid JSON escaping when including quotation marks, line breaks, code, or special characters.
- Never translate or rename these JSON properties:
  theoreticalQuestions,
  practicalQuestions,
  questionNumber,
  question,
  howToAnswer.
- Never return markdown fences, notes, commentary, headings outside the JSON, or extra text.
- Return valid JSON only.

JOB TITLE:
{jobTitle}

COMPANY:
{companyName}

JOB DESCRIPTION:
{preparedJobDescription}";

    // Sends one request to the existing OpenAI Responses API helper.
    string outputText =
        await SendPromptToOpenAIAsync(prompt);

    // Converts the returned JSON into the interview-question DTO.
    AIInterviewQuestionsResult? result =
        JsonSerializer.Deserialize<AIInterviewQuestionsResult>(
            outputText,
            JsonOptions
        );

    // Rejects an invalid or empty OpenAI result.
    if (result == null)
    {
        throw new Exception(
            "OpenAI returned invalid interview-question JSON."
        );
    }

    // Returns the structured result to GeneratedInterviewQuestionsService.
    return result;
}
        /// <summary>
        /// Classifies multiple jobs in one OpenAI request.
        ///
        /// OpenAI analyzes BOTH title and description and determines:
        /// - EmploymentType: Full-time, Part-time, Contract, or Internship.
        /// - WorkMode: On-site, Remote, or Hybrid.
        ///
        /// Classification caching and SQL persistence remain the responsibility
        /// of JobSearchService.
        /// </summary>
        public async Task<List<AIJobClassificationItem>>
            ClassifyJobsAsync(
                IReadOnlyCollection<Job> jobs)
        {
            if (jobs == null || jobs.Count == 0)
            {
                return new List<AIJobClassificationItem>();
            }

            var jobInputs =
                jobs.Select(job => new
                {
                    jobId = job.JobId,
                    title = job.Title,
                    description = PrepareJobDescription(
                        job.Description,
                        DocumentJobDescriptionLimit
                    )
                });

            string jobsJson =
                JsonSerializer.Serialize(jobInputs);

            string prompt = $@"
Classify every supplied job using BOTH its title and description.

Return JSON only in this exact format:
{{
  ""jobs"": [
    {{
      ""jobId"": 1,
      ""employmentType"": ""Full-time"",
      ""workMode"": ""Remote""
    }}
  ]
}}

EMPLOYMENT TYPE:
Allowed values only:
- Full-time
- Part-time
- Contract
- Internship

Employment rules:
- Internship includes intern, internship, trainee, apprenticeship, co-op, and student-placement roles when clearly indicated.
- Contract includes contractor, freelance, temporary, consulting engagement, and fixed-term work when clearly indicated.
- Part-time is used only when reduced or part-time hours are indicated.
- Permanent employment normally means Full-time unless part-time is explicitly stated.
- If employment type cannot be determined, return Full-time.

WORK MODE:
Allowed values only:
- On-site
- Remote
- Hybrid

Work-mode rules:
- Remote includes fully remote, work from home, WFH, home-based, or work from anywhere.
- Hybrid requires a combination of remote and workplace attendance.
- On-site includes office-based, site-based, in-person, or location-dependent work.
- Do not classify a job as Remote merely because remote collaboration tools are mentioned.
- If work mode cannot be determined, return On-site.

OUTPUT RULES:
- Analyze both title and description.
- If title and description conflict, trust the clearest explicit statement in the description.
- Return exactly one object for every supplied job.
- Copy every supplied jobId exactly.
- Use only the allowed values with the exact spelling and capitalization shown above.
- Never return explanations, markdown, notes, or additional properties.
- Return valid JSON immediately.

JOBS:
{jobsJson}";

            string outputText =
                await SendPromptToOpenAIAsync(prompt);

            AIJobClassificationResult response;

            try
            {
                response =
                    JsonSerializer.Deserialize<AIJobClassificationResult>(
                        outputText,
                        JsonOptions
                    )
                    ?? throw new JsonException(
                        "The classification response was null."
                    );
            }
            catch (JsonException exception)
            {
                throw new Exception(
                    "OpenAI returned invalid job-classification JSON.",
                    exception
                );
            }

            var validJobIds =
                jobs
                    .Select(job => job.JobId)
                    .ToHashSet();

            return response.Jobs
                .Where(item =>
                    validJobIds.Contains(item.JobId)
                )
                .GroupBy(item =>
                    item.JobId
                )
                .Select(group =>
                    group.First()
                )
                .ToList();
        }

        private async Task<string> SendPromptToOpenAIAsync(
            string prompt)
        {
            // Read the API key.
            string apiKey =
                _configuration["OpenAI:ApiKey"]
                ?? string.Empty;

            // Read the single model.
            // The old working fallback is restored.
            string model =
                _configuration["OpenAI:Model"]
                ?? "gpt-4.1-mini";

            // Stop immediately if the API key is missing.
            if (string.IsNullOrWhiteSpace(apiKey) ||
                apiKey == "ApiKey")
            {
                throw new Exception(
                    "OpenAI API key is missing in appsettings.json."
                );
            }

            // The Responses API requires the field name to be exactly "model".
            var requestBody = new
            {
                model,
                input = prompt
            };

            // Create the HTTP POST request.
            using var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://api.openai.com/v1/responses"
                );

            // Add the API key as a Bearer token.
            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    apiKey
                );

            // Serialize the request body as JSON.
            request.Content =
                new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

            // Send the request.
            using var response =
                await _httpClient.SendAsync(request);

            // Read the complete OpenAI response.
            string responseString =
                await response.Content.ReadAsStringAsync();

            // Throw the actual API error instead of silently hiding it.
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"OpenAI API error: {responseString}"
                );
            }

            // Extract the text returned by the model.
            string outputText =
                ExtractOutputText(responseString);

            if (string.IsNullOrWhiteSpace(outputText))
            {
                throw new Exception(
                    "OpenAI returned empty output."
                );
            }

            // Remove accidental markdown fences.
            return CleanJsonOutput(outputText);
        }

        /// <summary>
        /// Extracts the generated text from the Responses API JSON structure.
        /// </summary>
        private static string ExtractOutputText(
            string responseString)
        {
            using var json =
                JsonDocument.Parse(responseString);

            // Some responses may include output_text directly.
            if (json.RootElement.TryGetProperty(
                    "output_text",
                    out var directOutputText))
            {
                return directOutputText.GetString()
                    ?? string.Empty;
            }

            // Otherwise, read the output array.
            if (!json.RootElement.TryGetProperty(
                    "output",
                    out var output))
            {
                return string.Empty;
            }

            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty(
                        "content",
                        out var content))
                {
                    continue;
                }

                foreach (var contentItem
                    in content.EnumerateArray())
                {
                    if (contentItem.TryGetProperty(
                            "text",
                            out var text))
                    {
                        return text.GetString()
                            ?? string.Empty;
                    }
                }
            }

            return string.Empty;
        }
public async Task<string> TranslateRoleForCountryAsync(
    string role,
    string country)
{
    if (string.IsNullOrWhiteSpace(role))
    {
        return role;
    }

    string prompt = $@"
Translate the user's job role into the required language for the selected country.

Country and required language mapping:

- Lebanon: English
- Saudi Arabia: English
- United Arab Emirates: English
- Qatar: English
- Kuwait: English
- Oman: English
- Bahrain: English
- Jordan: English
- Iraq: English
- Egypt: English
- Morocco: French
- Tunisia: French
- United States: English
- Canada: English
- Mexico: Spanish
- Brazil: Portuguese
- United Kingdom: English
- France: French
- Italy: Italian
- Spain: Spanish
- India: English
- Japan: Japanese

Selected country:
{country}

User's job role:
{role}

Rules:
- Find the selected country in the mapping above.
- Translate the role into that country's required language.
- If the role is already written in the required language, return it unchanged.
- Preserve technical terms such as .NET, C#, Java, JavaScript, React, SQL, AWS, Azure, DevOps, and Node.js.
- Return only the final role.
- Do not return JSON.
- Do not add quotes.
- Do not add labels or explanations.
- Keep the result short and suitable for a job-search query.
";

    string translatedRole =
        await SendPromptToOpenAIAsync(prompt);

    translatedRole = translatedRole
        .Trim()
        .Trim('"');

    return string.IsNullOrWhiteSpace(translatedRole)
        ? role
        : translatedRole;
}
        /// <summary>
        /// Removes markdown code fences when OpenAI accidentally wraps JSON.
        /// </summary>
        private static string CleanJsonOutput(
            string outputText)
        {
            string cleaned = outputText.Trim();

            if (cleaned.StartsWith(
                    "```json",
                    StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[7..];
            }
            else if (cleaned.StartsWith("```"))
            {
                cleaned = cleaned[3..];
            }

            if (cleaned.EndsWith("```"))
            {
                cleaned = cleaned[..^3];
            }

            return cleaned.Trim();
        }

        /// <summary>
        /// Cleans and shortens a job description.
        ///
        /// Both the beginning and end are preserved because:
        /// - The beginning usually contains the role summary.
        /// - The end often contains requirements and qualifications.
        /// </summary>
        private static string PrepareJobDescription(
            string? description,
            int maximumLength)
        {
            string cleaned = CleanText(description);

            if (cleaned.Length <= maximumLength)
                return cleaned;

            // Keep 45% from the beginning.
            int beginningLength =
                maximumLength * 45 / 100;

            // Keep the remaining 55% from the end.
            int endingLength =
                maximumLength - beginningLength;

            return cleaned[..beginningLength]
                + " ... "
                + cleaned[^endingLength..];
        }

        /// <summary>
        /// Removes unnecessary spaces, line breaks, and tabs.
        /// This reduces prompt size without changing the content.
        /// </summary>
        private static string CleanText(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return string.Join(
                " ",
                value.Split(
                    new[] { ' ', '\r', '\n', '\t' },
                    StringSplitOptions.RemoveEmptyEntries
                )
            );
        }
    }
}