using CareerMatch.API.Data;
using CareerMatch.API.DTOs;
using Dapper;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CareerMatch.API.Services
{
    // Generates interview preparation and returns its PDF in one operation.
    public class GeneratedInterviewQuestionsService
    {
        // Creates SQL Server connections for Dapper.
        private readonly DbConnectionFactory _dbConnectionFactory;

        // Calls OpenAI.
        private readonly AIService _aiService;

        // Provides the application root folder.
        private readonly IWebHostEnvironment _environment;

        // Serializes the structured question result.
        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                // Allows case-insensitive property matching.
                PropertyNameCaseInsensitive = true
            };

        // Receives all required dependencies.
        public GeneratedInterviewQuestionsService(
            DbConnectionFactory dbConnectionFactory,
            AIService aiService,
            IWebHostEnvironment environment)
        {
            // Saves the database factory.
            _dbConnectionFactory = dbConnectionFactory;

            // Saves the AI service.
            _aiService = aiService;

            // Saves the hosting environment.
            _environment = environment;
        }

        // Generates, saves, converts, and returns one interview-preparation PDF.
        public async Task<GeneratedDocumentDownloadResult?>
            GenerateAndDownloadForApplicationAsync(
                int authenticatedUserId,
                int applicationId)
        {
            // Opens one database connection.
            using var connection =
                _dbConnectionFactory.CreateConnection();

            // Loads the exact application, CV role, and job.
            var applicationData =
                await connection.QueryFirstOrDefaultAsync<
                    InterviewApplicationData>(
                    @"
                    SELECT
                        ja.ApplicationId,
                        ja.CVId,
                        cv.PrimaryRole AS CandidatePrimaryRole,
                        j.Title AS JobTitle,
                        j.CompanyName,
                        j.Description AS JobDescription
                    FROM JobApplications ja
                    LEFT JOIN CVs cv
                        ON ja.CVId = cv.CVId
                    INNER JOIN Jobs j
                        ON ja.JobId = j.JobId
                    WHERE ja.ApplicationId = @ApplicationId
                      AND ja.UserId = @UserId;
                    ",
                    new
                    {
                        // Passes the application id safely.
                        ApplicationId = applicationId,
                        UserId = authenticatedUserId
                    }
                );

            // Returns null when the application does not exist.
            if (applicationData == null)
                return null;

            // Rejects an empty job description.
            if (string.IsNullOrWhiteSpace(
                applicationData.JobDescription))
            {
                throw new Exception(
                    "The selected job contains no usable description."
                );
            }

            // Candidate CV data is optional for interview-question generation.
            // When no CV exists, questions are generated from the job itself.
            var candidateSkills =
                new List<InterviewCandidateSkillData>();

            if (applicationData.CVId.HasValue)
            {
                candidateSkills =
                    (
                        await connection.QueryAsync<
                            InterviewCandidateSkillData>(
                            @"
                            SELECT
                                s.SkillName,
                                ecs.YearsOfExperience
                            FROM ExtractedCVSkills ecs
                            INNER JOIN Skills s
                                ON ecs.SkillId = s.SkillId
                            WHERE ecs.CVId = @CVId
                            ORDER BY s.SkillName;
                            ",
                            new
                            {
                                CVId = applicationData.CVId.Value
                            }
                        )
                    ).ToList();
            }

            string candidateSkillsText =
                applicationData.CVId.HasValue
                    ? BuildCandidateSkillsText(candidateSkills)
                    : "No CareerMatch CV was uploaded. Base the questions on the job title and description only.";

            string candidatePrimaryRole =
                string.IsNullOrWhiteSpace(
                    applicationData.CandidatePrimaryRole)
                    ? applicationData.JobTitle
                    : applicationData.CandidatePrimaryRole;

            // Generates exactly five theoretical and five practical questions.
            AIInterviewQuestionsResult questions =
                await _aiService
                    .GenerateInterviewQuestionsAsync(
                        candidatePrimaryRole,
                        candidateSkillsText,
                        applicationData.JobTitle,
                        applicationData.CompanyName,
                        applicationData.JobDescription
                    );

            // Confirms that all required content exists.
            ValidateAIResult(questions);

            // Applies stable numbering from 1 to 10.
            NormalizeQuestionNumbers(
                questions
            );

            // Converts the complete result into JSON for SQL Server.
            string generatedQuestionsJson =
                JsonSerializer.Serialize(
                    questions,
                    JsonOptions
                );

            // Uses one generation timestamp.
            DateTime generatedAt = DateTime.Now;

            // Creates the PDF folder.
            string pdfFolder =
                Path.Combine(
                    _environment.ContentRootPath,
                    "Uploads",
                    "GeneratedInterviewQuestions"
                );

            // Ensures that the folder exists.
            Directory.CreateDirectory(
                pdfFolder
            );

            // Creates safe filename segments.
            string safeJobTitle =
                CreateSafeFileName(
                    applicationData.JobTitle
                );

            // Creates a safe company segment.
            string safeCompanyName =
                CreateSafeFileName(
                    applicationData.CompanyName
                );

            // Creates a unique PDF filename.
            string pdfFileName =
                $"Interview_Preparation_{safeJobTitle}_{safeCompanyName}_{Guid.NewGuid():N}.pdf";

            // Builds the full PDF path.
            string pdfFilePath =
                Path.Combine(
                    pdfFolder,
                    pdfFileName
                );

            // Creates the QuestPDF document.
            CreatePdf(
                questions,
                applicationData.JobTitle,
                applicationData.CompanyName,
                pdfFilePath
            );

            // Checks whether this application already has a current question set.
            int? existingId =
                await connection.QueryFirstOrDefaultAsync<int?>(
                    @"
                    SELECT TOP 1
                        GeneratedInterviewQuestionId
                    FROM GeneratedInterviewQuestions
                    WHERE ApplicationId = @ApplicationId
                    ORDER BY GeneratedAt DESC;
                    ",
                    new
                    {
                        // Searches by application.
                        ApplicationId =
                            applicationId
                    }
                );

            // Updates the current row when one exists.
            if (existingId.HasValue)
            {
                await connection.ExecuteAsync(
                    @"
                    UPDATE GeneratedInterviewQuestions
                    SET
                        GeneratedQuestions =
                            @GeneratedQuestions,
                        GeneratedAt =
                            @GeneratedAt
                    WHERE GeneratedInterviewQuestionId =
                        @GeneratedInterviewQuestionId;
                    ",
                    new
                    {
                        // Selects the row.
                        GeneratedInterviewQuestionId =
                            existingId.Value,

                        // Stores structured JSON.
                        GeneratedQuestions =
                            generatedQuestionsJson,

                        // Stores generation time.
                        GeneratedAt =
                            generatedAt
                    }
                );
            }
            else
            {
                // Inserts the first question set for this application.
                await connection.ExecuteAsync(
                    @"
                    INSERT INTO GeneratedInterviewQuestions
                    (
                        ApplicationId,
                        GeneratedQuestions,
                        GeneratedAt
                    )
                    VALUES
                    (
                        @ApplicationId,
                        @GeneratedQuestions,
                        @GeneratedAt
                    );
                    ",
                    new
                    {
                        // Links the set to the application.
                        ApplicationId =
                            applicationId,

                        // Stores structured JSON.
                        GeneratedQuestions =
                            generatedQuestionsJson,

                        // Stores generation time.
                        GeneratedAt =
                            generatedAt
                    }
                );
            }

            // Reads the completed PDF.
            byte[] fileBytes =
                await File.ReadAllBytesAsync(
                    pdfFilePath
                );

            // Returns only the downloadable document.
            return new GeneratedDocumentDownloadResult
            {
                // Returns PDF bytes.
                FileBytes = fileBytes,

                // Returns the browser filename.
                FileName = pdfFileName,

                // Returns the PDF content type.
                ContentType = "application/pdf"
            };
        }

        // Creates the interview-preparation PDF.
        private static void CreatePdf(
            AIInterviewQuestionsResult questions,
            string jobTitle,
            string companyName,
            string pdfFilePath)
        {
            // Creates the QuestPDF document.
            Document.Create(document =>
            {
                // Defines the page layout.
                document.Page(page =>
                {
                    // Uses A4 paper.
                    page.Size(PageSizes.A4);

                    // Adds margins.
                    page.Margin(40);

                    // Sets default typography.
                  page.DefaultTextStyle(style =>
                        style
                            .FontFamily("Noto Sans Arabic")
                            .FontSize(10)
                            .LineHeight(1.3f)
                    );

                    // Adds the repeated header.
                    page.Header()
                        .PaddingBottom(10)
                        .BorderBottom(1)
                        .Column(header =>
                        {
                            // Adds the document title.
                            header.Item()
                                .Text(
                                    "Interview Preparation"
                                )
                                .SemiBold()
                                .FontSize(16);

                            // Adds the job context.
                            header.Item()
                                .Text(
                                    $"{jobTitle} at {companyName}"
                                )
                                .FontSize(10);
                        });

                    // Adds the document content.
                    page.Content()
                        .PaddingVertical(15)
                        .Column(column =>
                        {
                            // Adds spacing between blocks.
                            column.Spacing(10);

                            // Adds the practice title.
                            column.Item()
                                .Text(
                                    "Questions — Practice First"
                                )
                                .Bold()
                                .FontSize(14);

                            // Adds theoretical questions only.
                            AddQuestionSection(
                                column,
                                "Theoretical Questions",
                                questions.TheoreticalQuestions
                            );

                            // Adds practical questions only.
                            AddQuestionSection(
                                column,
                                "Practical Questions",
                                questions.PracticalQuestions
                            );

                            // Forces all answers to begin on a different page.
                            column.Item()
                                .PageBreak();

                            // Adds the answer-section title.
                            column.Item()
                                .Text(
                                    "Suggested Answers and Guidance"
                                )
                                .Bold()
                                .FontSize(14);

                            // Adds theoretical answers.
                            AddAnswerSection(
                                column,
                                "Theoretical Answers",
                                questions.TheoreticalQuestions
                            );

                            // Adds practical solutions.
                            AddAnswerSection(
                                column,
                                "Practical Solutions",
                                questions.PracticalQuestions
                            );
                        });

                    // Adds page numbering.
                    page.Footer()
                        .AlignCenter()
                        .Text(text =>
                        {
                            text.Span("Page ");
                            text.CurrentPageNumber();
                            text.Span(" of ");
                            text.TotalPages();
                        });
                });
            })
            // Writes the PDF to disk.
            .GeneratePdf(pdfFilePath);
        }

        // Adds one question-only section.
        private static void AddQuestionSection(
            ColumnDescriptor column,
            string heading,
            IReadOnlyCollection<
                InterviewQuestionItem> questions)
        {
            // Adds the section heading.
            column.Item()
                .PaddingTop(5)
                .Text(heading)
                .SemiBold()
                .FontSize(12);

            // Adds every question without its answer.
            foreach (InterviewQuestionItem item
                in questions)
            {
                column.Item()
                    .Text(
                        $"{item.QuestionNumber}. {item.Question}"
                    );
            }
        }

        // Adds one answer section.
        private static void AddAnswerSection(
            ColumnDescriptor column,
            string heading,
            IReadOnlyCollection<
                InterviewQuestionItem> questions)
        {
            // Adds the section heading.
            column.Item()
                .PaddingTop(5)
                .Text(heading)
                .SemiBold()
                .FontSize(12);

            // Adds every answer block.
            foreach (InterviewQuestionItem item
                in questions)
            {
                column.Item()
                    .PaddingBottom(8)
                    .Column(answer =>
                    {
                        // Repeats the question.
                        answer.Item()
                            .Text(
                                $"{item.QuestionNumber}. {item.Question}"
                            )
                            .SemiBold();

                        // Adds the suggested solution.
                        answer.Item()
                            .PaddingTop(3)
                            .Text(
                                $"Suggested answer: {item.SuggestedAnswer}"
                            );

                        // Adds answering guidance.
                        answer.Item()
                            .PaddingTop(3)
                            .Text(
                                $"How to answer: {item.HowToAnswer}"
                            );
                    });
            }
        }

        // Converts candidate skills into prompt text.
        private static string BuildCandidateSkillsText(
            List<InterviewCandidateSkillData> skills)
        {
            // Provides a clear fallback.
            if (skills.Count == 0)
            {
                return
                    "No extracted candidate skills were found.";
            }

            // Creates one line per skill.
            return string.Join(
                Environment.NewLine,
                skills.Select(skill =>
                    $"- {skill.SkillName}: " +
                    $"{skill.YearsOfExperience ?? 0} years"
                )
            );
        }

        // Confirms the exact required output.
        private static void ValidateAIResult(
            AIInterviewQuestionsResult result)
        {
            // Requires exactly five theoretical questions.
            if (result.TheoreticalQuestions.Count != 5)
            {
                throw new Exception(
                    "OpenAI did not return exactly five theoretical questions."
                );
            }

            // Requires exactly five practical questions.
            if (result.PracticalQuestions.Count != 5)
            {
                throw new Exception(
                    "OpenAI did not return exactly five practical questions."
                );
            }

            // Combines both groups.
            IEnumerable<InterviewQuestionItem>
                allQuestions =
                    result.TheoreticalQuestions
                        .Concat(
                            result.PracticalQuestions
                        );

            // Rejects incomplete question objects.
            if (allQuestions.Any(item =>
                    string.IsNullOrWhiteSpace(
                        item.Question) ||
                    string.IsNullOrWhiteSpace(
                        item.SuggestedAnswer) ||
                    string.IsNullOrWhiteSpace(
                        item.HowToAnswer)))
            {
                throw new Exception(
                    "OpenAI returned an incomplete interview question."
                );
            }
        }

        // Applies numbering from 1 through 10.
        private static void NormalizeQuestionNumbers(
            AIInterviewQuestionsResult result)
        {
            // Numbers theoretical questions 1 to 5.
            for (int index = 0;
                 index <
                    result.TheoreticalQuestions.Count;
                 index++)
            {
                result.TheoreticalQuestions[index]
                    .QuestionNumber =
                        index + 1;
            }

            // Numbers practical questions 6 to 10.
            for (int index = 0;
                 index <
                    result.PracticalQuestions.Count;
                 index++)
            {
                result.PracticalQuestions[index]
                    .QuestionNumber =
                        index + 6;
            }
        }

        // Creates a safe filename segment.
        private static string CreateSafeFileName(
            string value)
        {
            // Provides a fallback.
            if (string.IsNullOrWhiteSpace(value))
                return "Interview";

            // Replaces unsupported characters.
            string safeValue =
                Regex.Replace(
                    value.Trim(),
                    @"[^a-zA-Z0-9\-_]+",
                    "_"
                );

            // Collapses repeated underscores.
            safeValue =
                Regex.Replace(
                    safeValue,
                    @"_+",
                    "_"
                );

            // Limits segment length.
            if (safeValue.Length > 50)
            {
                safeValue =
                    safeValue.Substring(0, 50);
            }

            // Removes separators from both ends.
            safeValue =
                safeValue.Trim('_', '-');

            // Guarantees a non-empty result.
            return string.IsNullOrWhiteSpace(
                safeValue)
                ? "Interview"
                : safeValue;
        }

        // Holds application, CV, and job query data.
        private class InterviewApplicationData
        {
            // Stores the application id.
            public int ApplicationId { get; set; }

            // Stores the exact CV id.
            public int? CVId { get; set; }

            // Stores the candidate primary role.
            public string? CandidatePrimaryRole
            {
                get;
                set;
            }

            // Stores the job title.
            public string JobTitle { get; set; }
                = string.Empty;

            // Stores the company name.
            public string CompanyName { get; set; }
                = string.Empty;

            // Stores the full job description.
            public string JobDescription { get; set; }
                = string.Empty;
        }

        // Holds one candidate skill row.
        private class InterviewCandidateSkillData
        {
            // Stores the skill name.
            public string SkillName { get; set; }
                = string.Empty;

            // Stores known or unknown experience.
            public decimal? YearsOfExperience
            {
                get;
                set;
            }
        }
    }
}