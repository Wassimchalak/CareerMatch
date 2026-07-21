using CareerMatch.API.Data;
using CareerMatch.API.DTOs;
using Dapper;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.RegularExpressions;

namespace CareerMatch.API.Services
{
    // Generates a cover letter and returns its PDF in one operation.
    public class GeneratedCoverLetterService
    {
        // Creates SQL Server connections for Dapper.
        private readonly DbConnectionFactory _dbConnectionFactory;

        // Calls OpenAI.
        private readonly AIService _aiService;

        // Provides the application root folder.
        private readonly IWebHostEnvironment _environment;

        // Receives all required dependencies.
        public GeneratedCoverLetterService(
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

        // Generates, saves, converts, and returns one cover-letter PDF.
        public async Task<GeneratedDocumentDownloadResult?>
            GenerateAndDownloadForApplicationAsync(
                int authenticatedUserId,
                int applicationId)
        {
            // Opens one database connection.
            using var connection =
                _dbConnectionFactory.CreateConnection();

            // Loads the exact application, CV, and job.
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
                        // Passes the application id safely.
                        ApplicationId = applicationId,
                        UserId = authenticatedUserId
                    }
                );

            // Returns null when the application does not exist.
            if (applicationData == null)
                return null;

            // Prevents generation from an unreadable CV.
            if (string.IsNullOrWhiteSpace(
                applicationData.CandidateCVText))
            {
                throw new Exception(
                    "The CV used for this application contains no readable text."
                );
            }

            // Loads the candidate skills from the exact application CV.
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
                            // Uses the exact application CV.
                            CVId = applicationData.CVId
                        }
                    )
                ).ToList();

            // Converts skill rows into prompt text.
            string candidateSkillsText =
                BuildCandidateSkillsText(
                    candidateSkills
                );

            // Generates the cover letter with OpenAI.
            string coverLetterText =
                await _aiService.GenerateCoverLetterAsync(
                    applicationData.CandidateCVText,
                    candidateSkillsText,
                    applicationData.JobTitle,
                    applicationData.CompanyName,
                    applicationData.JobDescription
                );

            // Rejects an empty AI result.
            if (string.IsNullOrWhiteSpace(
                coverLetterText))
            {
                throw new Exception(
                    "OpenAI returned an empty cover letter."
                );
            }

            // Uses one generation timestamp.
            DateTime generatedAt = DateTime.Now;

            // Creates the PDF folder.
            string pdfFolder =
                Path.Combine(
                    _environment.ContentRootPath,
                    "Uploads",
                    "GeneratedCoverLetters"
                );

            // Ensures that the folder exists.
            Directory.CreateDirectory(pdfFolder);

            // Creates safe filename parts.
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
                $"Cover_Letter_{safeJobTitle}_{safeCompanyName}_{Guid.NewGuid():N}.pdf";

            // Builds the full PDF path.
            string pdfFilePath =
                Path.Combine(
                    pdfFolder,
                    pdfFileName
                );

            // Generates the QuestPDF document.
            CreatePdf(
                coverLetterText,
                pdfFilePath
            );

            // Updates the existing row or inserts the first row.
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
                        // Searches by application.
                        ApplicationId = applicationId
                    }
                );

            // Updates an existing current document.
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
                        // Selects the row.
                        GeneratedCoverLetterId =
                            existingId.Value,

                        // Stores generated text.
                        CoverLetterText =
                            coverLetterText,

                        // Stores the generated filename.
                        GeneratedPdfFileName =
                            pdfFileName,

                        // Stores the server path.
                        GeneratedPdfFilePath =
                            pdfFilePath,

                        // Stores generation time.
                        GeneratedAt =
                            generatedAt
                    }
                );
            }
            else
            {
                // Inserts the first generated cover letter for this application.
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
                        // Links the document to the application.
                        ApplicationId =
                            applicationId,

                        // Stores generated text.
                        CoverLetterText =
                            coverLetterText,

                        // Stores the generated filename.
                        GeneratedPdfFileName =
                            pdfFileName,

                        // Stores the server path.
                        GeneratedPdfFilePath =
                            pdfFilePath,

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

                // Returns the download filename.
                FileName = pdfFileName,

                // Returns the PDF content type.
                ContentType = "application/pdf"
            };
        }

        // Creates the QuestPDF cover-letter document.
        private static void CreatePdf(
            string coverLetterText,
            string pdfFilePath)
        {
            // Splits the letter into paragraphs.
            string[] paragraphs =
                coverLetterText
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n")
                    .Split(
                        "\n\n",
                        StringSplitOptions.RemoveEmptyEntries
                    );

            // Creates the PDF.
            Document.Create(document =>
            {
                // Defines one reusable page layout.
                document.Page(page =>
                {
                    // Uses A4 paper.
                    page.Size(PageSizes.A4);

                    // Adds margins.
                    page.Margin(45);

                    // Sets default typography.
                    page.DefaultTextStyle(style =>
                        style.FontSize(11)
                             .LineHeight(1.4f)
                    );

                    // Adds the header.
                    page.Header()
                        .PaddingBottom(10)
                        .BorderBottom(1)
                        .Text("Cover Letter")
                        .SemiBold()
                        .FontSize(16);

                    // Adds the letter body.
                    page.Content()
                        .PaddingVertical(20)
                        .Column(column =>
                        {
                            // Adds paragraph spacing.
                            column.Spacing(12);

                            // Adds every non-empty paragraph.
                            foreach (string rawParagraph
                                in paragraphs)
                            {
                                // Trims the paragraph.
                                string paragraph =
                                    rawParagraph.Trim();

                                // Skips empty paragraphs.
                                if (string.IsNullOrWhiteSpace(
                                    paragraph))
                                {
                                    continue;
                                }

                                // Writes the paragraph.
                                column.Item()
                                    .Text(paragraph);
                            }
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

        // Converts candidate skills into prompt text.
        private static string BuildCandidateSkillsText(
            List<CandidateSkillData> skills)
        {
            // Provides a clear fallback.
            if (skills.Count == 0)
            {
                return
                    "No extracted candidate skills were found.";
            }

            // Formats one line per skill.
            return string.Join(
                Environment.NewLine,
                skills.Select(skill =>
                    $"- {skill.SkillName}: " +
                    $"{skill.YearsOfExperience ?? 0} years"
                )
            );
        }

        // Creates a safe Windows filename segment.
        private static string CreateSafeFileName(
            string value)
        {
            // Provides a fallback.
            if (string.IsNullOrWhiteSpace(value))
                return "Document";

            // Removes unsupported characters.
            string safeValue =
                Regex.Replace(
                    value.Trim(),
                    @"[^a-zA-Z0-9\-_]+",
                    "_"
                );

            // Limits filename length.
            if (safeValue.Length > 50)
            {
                safeValue =
                    safeValue.Substring(0, 50);
            }

            // Returns the cleaned value.
            return safeValue.Trim('_');
        }

        // Holds Dapper query data.
        private class CoverLetterApplicationData
        {
            // Stores the application id.
            public int ApplicationId { get; set; }

            // Stores the exact CV id.
            public int CVId { get; set; }

            // Stores extracted CV text.
            public string CandidateCVText { get; set; }
                = string.Empty;

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
        private class CandidateSkillData
        {
            // Stores the normalized skill name.
            public string SkillName { get; set; }
                = string.Empty;

            // Stores known or unknown experience.
            public decimal? YearsOfExperience { get; set; }
        }
    }
}
