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
    public class GeneratedInterviewQuestionsService
    {
        private readonly DbConnectionFactory _dbConnectionFactory;

        private readonly AIService _aiService;

        private readonly IWebHostEnvironment _environment;

        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

        public GeneratedInterviewQuestionsService(
            DbConnectionFactory dbConnectionFactory,
            AIService aiService,
            IWebHostEnvironment environment)
        {
            _dbConnectionFactory = dbConnectionFactory;

            _aiService = aiService;

            _environment = environment;
        }

        public async Task<GeneratedDocumentDownloadResult?>
            GenerateAndDownloadForApplicationAsync(
                int authenticatedUserId,
                int applicationId)
        {
            using var connection =
                _dbConnectionFactory.CreateConnection();

            var applicationData =
                await connection.QueryFirstOrDefaultAsync<
                    InterviewApplicationData>(
                    @"
                    SELECT
                        ja.ApplicationId,
                        j.Title AS JobTitle,
                        j.CompanyName,
                        j.Description AS JobDescription
                    FROM JobApplications ja
                    INNER JOIN Jobs j
                        ON ja.JobId = j.JobId
                    WHERE ja.ApplicationId = @ApplicationId
                      AND ja.UserId = @UserId;
                    ",
                    new
                    {
                        ApplicationId = applicationId,

                        UserId = authenticatedUserId
                    }
                );

            if (applicationData == null)
                return null;

            if (string.IsNullOrWhiteSpace(
                applicationData.JobDescription))
            {
                throw new Exception(
                    "The selected job contains no usable description."
                );
            }

            AIInterviewQuestionsResult questions =
                await _aiService
                    .GenerateInterviewQuestionsAsync(
                        applicationData.JobTitle,
                        applicationData.CompanyName,
                        applicationData.JobDescription
                    );

            ValidateAIResult(questions);

            NormalizeQuestionNumbers(
                questions
            );

            string generatedQuestionsJson =
                JsonSerializer.Serialize(
                    questions,
                    JsonOptions
                );

            DateTime generatedAt = DateTime.Now;

            string pdfFolder =
                Path.Combine(
                    _environment.ContentRootPath,
                    "Uploads",
                    "GeneratedInterviewQuestions"
                );

            Directory.CreateDirectory(
                pdfFolder
            );

            string safeJobTitle =
                CreateSafeFileName(
                    applicationData.JobTitle
                );

            string safeCompanyName =
                CreateSafeFileName(
                    applicationData.CompanyName
                );

            string pdfFileName =
                $"Interview_Preparation_{safeJobTitle}_{safeCompanyName}_{Guid.NewGuid():N}.pdf";

            string pdfFilePath =
                Path.Combine(
                    pdfFolder,
                    pdfFileName
                );

            CreatePdf(
                questions,
                applicationData.JobTitle,
                applicationData.CompanyName,
                pdfFilePath
            );

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
                        ApplicationId = applicationId
                    }
                );

            if (existingId.HasValue)
            {
                await connection.ExecuteAsync(
                    @"
                    UPDATE GeneratedInterviewQuestions
                    SET
                        GeneratedQuestions = @GeneratedQuestions,
                        GeneratedAt = @GeneratedAt
                    WHERE GeneratedInterviewQuestionId =
                        @GeneratedInterviewQuestionId;
                    ",
                    new
                    {
                        GeneratedInterviewQuestionId =
                            existingId.Value,

                        GeneratedQuestions =
                            generatedQuestionsJson,

                        GeneratedAt =
                            generatedAt
                    }
                );
            }
            else
            {
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
                        ApplicationId =
                            applicationId,

                        GeneratedQuestions =
                            generatedQuestionsJson,

                        GeneratedAt =
                            generatedAt
                    }
                );
            }

            byte[] fileBytes =
                await File.ReadAllBytesAsync(
                    pdfFilePath
                );

            return new GeneratedDocumentDownloadResult
            {
                FileBytes = fileBytes,

                FileName = pdfFileName,

                ContentType = "application/pdf"
            };
        }

        private static void CreatePdf(
            AIInterviewQuestionsResult questions,
            string jobTitle,
            string companyName,
            string pdfFilePath)
        {
            Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);

                    page.Margin(40);

                    page.DefaultTextStyle(style =>
                        style
                            .FontFamily("Noto Sans Arabic")
                            .FontSize(10)
                            .LineHeight(1.3f)
                    );

                    page.Header()
                        .PaddingBottom(10)
                        .BorderBottom(1)
                        .Column(header =>
                        {
                            header.Item()
                                .Text(
                                    "Interview Preparation"
                                )
                                .SemiBold()
                                .FontSize(16);

                            header.Item()
                                .Text(
                                    $"{jobTitle} at {companyName}"
                                )
                                .FontSize(10);
                        });

                    page.Content()
                        .PaddingVertical(15)
                        .Column(column =>
                        {
                            column.Spacing(10);

                            column.Item()
                                .Text(
                                    "Questions — Practice First"
                                )
                                .Bold()
                                .FontSize(14);

                            AddQuestionSection(
                                column,
                                "Theoretical Questions",
                                questions.TheoreticalQuestions
                            );

                            AddQuestionSection(
                                column,
                                "Practical Questions",
                                questions.PracticalQuestions
                            );

                            column.Item()
                                .PageBreak();

                            column.Item()
                                .Text("Interview Guidance")
                                .Bold()
                                .FontSize(14);

                        

                            AddAnswerSection(
                                column,
                                "Theoretical Questions",
                                questions.TheoreticalQuestions
                            );

                            AddAnswerSection(
                                column,
                                "Practical Questions",
                                questions.PracticalQuestions
                            );
                        });

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
            .GeneratePdf(pdfFilePath);
        }

        private static void AddQuestionSection(
            ColumnDescriptor column,
            string heading,
            IReadOnlyCollection<
                InterviewQuestionItem> questions)
        {
            column.Item()
                .PaddingTop(5)
                .Text(heading)
                .SemiBold()
                .FontSize(12);

            foreach (InterviewQuestionItem item
                in questions)
            {
                column.Item()
                    .Text(
                        $"{item.QuestionNumber}. {item.Question}"
                    );
            }
        }

        private static void AddAnswerSection(
            ColumnDescriptor column,
            string heading,
            IReadOnlyCollection<
                InterviewQuestionItem> questions)
        {
            column.Item()
                .PaddingTop(5)
                .Text(heading)
                .SemiBold()
                .FontSize(12);

            foreach (InterviewQuestionItem item
                in questions)
            {
                column.Item()
                    .PaddingBottom(8)
                    .Column(guidance =>
                    {
                        guidance.Item()
                            .Text(
                                $"{item.QuestionNumber}. {item.Question}"
                            )
                            .SemiBold();

                       

                        guidance.Item()
                            .PaddingTop(3)
                            .Text(
                                $"How to answer: {item.HowToAnswer}"
                            );
                    });
            }
        }

        private static void ValidateAIResult(
            AIInterviewQuestionsResult result)
        {
            if (result.TheoreticalQuestions.Count != 5)
            {
                throw new Exception(
                    "OpenAI did not return exactly five theoretical questions."
                );
            }

            if (result.PracticalQuestions.Count != 5)
            {
                throw new Exception(
                    "OpenAI did not return exactly five practical questions."
                );
            }

            IEnumerable<InterviewQuestionItem>
                allQuestions =
                    result.TheoreticalQuestions
                        .Concat(
                            result.PracticalQuestions
                        );

            if (allQuestions.Any(item =>
                    string.IsNullOrWhiteSpace(
                        item.Question) ||
                    
                    string.IsNullOrWhiteSpace(
                        item.HowToAnswer)))
            {
                throw new Exception(
                    "OpenAI returned an incomplete interview question."
                );
            }
        }

        private static void NormalizeQuestionNumbers(
            AIInterviewQuestionsResult result)
        {
            for (int index = 0;
                 index <
                    result.TheoreticalQuestions.Count;
                 index++)
            {
                result.TheoreticalQuestions[index]
                    .QuestionNumber =
                        index + 1;
            }

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

        private static string CreateSafeFileName(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Interview";

            string safeValue =
                Regex.Replace(
                    value.Trim(),
                    @"[^a-zA-Z0-9\-_]+",
                    "_"
                );

            safeValue =
                Regex.Replace(
                    safeValue,
                    @"_+",
                    "_"
                );

            if (safeValue.Length > 50)
            {
                safeValue =
                    safeValue.Substring(0, 50);
            }

            safeValue =
                safeValue.Trim('_', '-');

            return string.IsNullOrWhiteSpace(
                safeValue)
                ? "Interview"
                : safeValue;
        }

        private class InterviewApplicationData
        {
            public int ApplicationId { get; set; }

            public string JobTitle { get; set; }
                = string.Empty;

            public string CompanyName { get; set; }
                = string.Empty;

            public string JobDescription { get; set; }
                = string.Empty;
        }
    }
}