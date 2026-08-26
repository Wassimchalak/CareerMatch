using CareerMatch.API.Data;
using CareerMatch.API.DTOs;
using Dapper;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.RegularExpressions;

namespace CareerMatch.API.Services
{
    public class GeneratedCoverLetterService
    {
        private readonly DbConnectionFactory _dbConnectionFactory;

        private readonly AIService _aiService;

        private readonly IWebHostEnvironment _environment;

        public GeneratedCoverLetterService(
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
                    CoverLetterApplicationData>(
                    @"
                    SELECT
                        ja.ApplicationId,
                        ja.CVId,
                        cv.ExtractedText AS CandidateCVText,
                        j.Title AS JobTitle,
                        j.CompanyName,
                        j.Description AS JobDescription
                    FROM JobApplications ja
                    INNER JOIN CVs cv
                        ON ja.CVId = cv.CVId
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
                applicationData.CandidateCVText))
            {
                throw new Exception(
                    "The CV used for this application contains no readable text."
                );
            }

            var candidateSkills =
                (
                    await connection.QueryAsync<
                        CandidateSkillData>(
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
                            CVId = applicationData.CVId
                        }
                    )
                ).ToList();

            string candidateSkillsText =
                BuildCandidateSkillsText(
                    candidateSkills
                );

            string coverLetterText =
                await _aiService.GenerateCoverLetterAsync(
                    applicationData.CandidateCVText,
                    candidateSkillsText,
                    applicationData.JobTitle,
                    applicationData.CompanyName,
                    applicationData.JobDescription
                );

            if (string.IsNullOrWhiteSpace(
                coverLetterText))
            {
                throw new Exception(
                    "OpenAI returned an empty cover letter."
                );
            }

            DateTime generatedAt = DateTime.Now;

            string pdfFolder =
                Path.Combine(
                    _environment.ContentRootPath,
                    "Uploads",
                    "GeneratedCoverLetters"
                );

            Directory.CreateDirectory(pdfFolder);

            string safeJobTitle =
                CreateSafeFileName(
                    applicationData.JobTitle
                );

            string safeCompanyName =
                CreateSafeFileName(
                    applicationData.CompanyName
                );

            string pdfFileName =
                $"Cover_Letter_{safeJobTitle}_{safeCompanyName}_{Guid.NewGuid():N}.pdf";

            string pdfFilePath =
                Path.Combine(
                    pdfFolder,
                    pdfFileName
                );

            CreatePdf(
                coverLetterText,
                pdfFilePath
            );

            int? existingId =
                await connection.QueryFirstOrDefaultAsync<int?>(
                    @"
                    SELECT TOP 1 GeneratedCoverLetterId
                    FROM GeneratedCoverLetters
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
                    UPDATE GeneratedCoverLetters
                    SET
                        CoverLetterText = @CoverLetterText,
                        GeneratedPdfFileName = @GeneratedPdfFileName,
                        GeneratedPdfFilePath = @GeneratedPdfFilePath,
                        GeneratedAt = @GeneratedAt
                    WHERE GeneratedCoverLetterId =
                        @GeneratedCoverLetterId;
                    ",
                    new
                    {
                        GeneratedCoverLetterId =
                            existingId.Value,

                        CoverLetterText =
                            coverLetterText,

                        GeneratedPdfFileName =
                            pdfFileName,

                        GeneratedPdfFilePath =
                            pdfFilePath,

                        GeneratedAt =
                            generatedAt
                    }
                );
            }
            else
            {
                await connection.ExecuteAsync(
                    @"
                    INSERT INTO GeneratedCoverLetters
                    (
                        ApplicationId,
                        CoverLetterText,
                        GeneratedPdfFileName,
                        GeneratedPdfFilePath,
                        GeneratedAt
                    )
                    VALUES
                    (
                        @ApplicationId,
                        @CoverLetterText,
                        @GeneratedPdfFileName,
                        @GeneratedPdfFilePath,
                        @GeneratedAt
                    );
                    ",
                    new
                    {
                        ApplicationId =
                            applicationId,

                        CoverLetterText =
                            coverLetterText,

                        GeneratedPdfFileName =
                            pdfFileName,

                        GeneratedPdfFilePath =
                            pdfFilePath,

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
            string coverLetterText,
            string pdfFilePath)
        {
            string[] paragraphs =
                coverLetterText
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n")
                    .Split(
                        "\n\n",
                        StringSplitOptions.RemoveEmptyEntries
                    );

            Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);

                    page.Margin(45);

                    page.DefaultTextStyle(style =>
                        style.FontSize(11)
                             .LineHeight(1.4f)
                    );

                    page.Header()
                        .PaddingBottom(10)
                        .BorderBottom(1)
                        .Text("Cover Letter")
                        .SemiBold()
                        .FontSize(16);

                    page.Content()
                        .PaddingVertical(20)
                        .Column(column =>
                        {
                            column.Spacing(12);

                            foreach (string rawParagraph
                                in paragraphs)
                            {
                                string paragraph =
                                    rawParagraph.Trim();

                                if (string.IsNullOrWhiteSpace(
                                    paragraph))
                                {
                                    continue;
                                }

                                column.Item()
                                    .Text(paragraph);
                            }
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

        private static string BuildCandidateSkillsText(
            List<CandidateSkillData> skills)
        {
            if (skills.Count == 0)
            {
                return
                    "No extracted candidate skills were found.";
            }

            return string.Join(
                Environment.NewLine,
                skills.Select(skill =>
                    $"- {skill.SkillName}: " +
                    $"{skill.YearsOfExperience ?? 0} years"
                )
            );
        }

        private static string CreateSafeFileName(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Document";

            string safeValue =
                Regex.Replace(
                    value.Trim(),
                    @"[^a-zA-Z0-9\-_]+",
                    "_"
                );

            if (safeValue.Length > 50)
            {
                safeValue =
                    safeValue.Substring(0, 50);
            }

            return safeValue.Trim('_');
        }

        private class CoverLetterApplicationData
        {
            public int ApplicationId { get; set; }

            public int CVId { get; set; }

            public string CandidateCVText { get; set; }
                = string.Empty;

            public string JobTitle { get; set; }
                = string.Empty;

            public string CompanyName { get; set; }
                = string.Empty;

            public string JobDescription { get; set; }
                = string.Empty;
        }

        private class CandidateSkillData
        {
            public string SkillName { get; set; }
                = string.Empty;

            public decimal? YearsOfExperience { get; set; }
        }
    }
}