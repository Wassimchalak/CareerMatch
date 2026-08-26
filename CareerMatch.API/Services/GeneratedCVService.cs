using CareerMatch.API.Data;
using CareerMatch.API.DTOs;
using Dapper;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.RegularExpressions;

namespace CareerMatch.API.Services
{
    public class GeneratedCVService
    {
        private readonly DbConnectionFactory _dbConnectionFactory;

        private readonly AIService _aiService;

        private readonly IWebHostEnvironment _environment;

        public GeneratedCVService(
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
                    ApplicationRefinementData>(
                    @"
                    SELECT
                        ja.ApplicationId,
                        ja.CVId,
                        cv.ExtractedText AS OriginalCVText,
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
                applicationData.OriginalCVText))
            {
                throw new Exception(
                    "The original CV contains no readable text."
                );
            }

            var cvSkills =
                (
                    await connection.QueryAsync<CVSkillData>(
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

            string cvSkillsText =
                BuildCVSkillsText(cvSkills);

            string generatedCVText =
                await _aiService.RefineCVForJobAsync(
                    applicationData.OriginalCVText,
                    cvSkillsText,
                    applicationData.JobTitle,
                    applicationData.CompanyName,
                    applicationData.JobDescription
                );

            if (string.IsNullOrWhiteSpace(
                generatedCVText))
            {
                throw new Exception(
                    "OpenAI returned an empty refined CV."
                );
            }

            DateTime generatedAt = DateTime.UtcNow;

            string pdfFolder =
                Path.Combine(
                    _environment.ContentRootPath,
                    "Uploads",
                    "GeneratedCVs"
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
                $"Refined_CV_{safeJobTitle}_{safeCompanyName}_{Guid.NewGuid():N}.pdf";

            string pdfFilePath =
                Path.Combine(
                    pdfFolder,
                    pdfFileName
                );

            List<string> lines =
                PrepareCVLines(
                    generatedCVText
                );

            CreatePdf(
                lines,
                pdfFilePath
            );

            ExistingGeneratedCVData?
                existingGeneratedCV = null;

            try
            {
                if (connection.State !=
                    System.Data.ConnectionState.Open)
                {
                    connection.Open();
                }

                using var transaction =
                    connection.BeginTransaction();

                try
                {
                    existingGeneratedCV =
                        await connection.QueryFirstOrDefaultAsync<
                            ExistingGeneratedCVData>(
                            @"
                            SELECT TOP 1
                                GeneratedCVId,
                                GeneratedPdfFileName,
                                GeneratedPdfFilePath
                            FROM GeneratedCVs
                                WITH (UPDLOCK, HOLDLOCK)
                            WHERE ApplicationId =
                                @ApplicationId
                            ORDER BY GeneratedAt DESC;
                            ",
                            new
                            {
                                ApplicationId =
                                    applicationId
                            },
                            transaction
                        );

                    if (existingGeneratedCV == null)
                    {
                        await connection.ExecuteAsync(
                            @"
                            INSERT INTO GeneratedCVs
                            (
                                ApplicationId,
                                GeneratedCVText,
                                GeneratedPdfFileName,
                                GeneratedPdfFilePath,
                                GeneratedAt
                            )
                            VALUES
                            (
                                @ApplicationId,
                                @GeneratedCVText,
                                @GeneratedPdfFileName,
                                @GeneratedPdfFilePath,
                                @GeneratedAt
                            );
                            ",
                            new
                            {
                                ApplicationId =
                                    applicationId,

                                GeneratedCVText =
                                    generatedCVText,

                                GeneratedPdfFileName =
                                    pdfFileName,

                                GeneratedPdfFilePath =
                                    pdfFilePath,

                                GeneratedAt =
                                    generatedAt
                            },
                            transaction
                        );
                    }
                    else
                    {
                        await connection.ExecuteAsync(
                            @"
                            UPDATE GeneratedCVs
                            SET
                                GeneratedCVText =
                                    @GeneratedCVText,

                                GeneratedPdfFileName =
                                    @GeneratedPdfFileName,

                                GeneratedPdfFilePath =
                                    @GeneratedPdfFilePath,

                                GeneratedAt =
                                    @GeneratedAt
                            WHERE GeneratedCVId =
                                @GeneratedCVId;
                            ",
                            new
                            {
                                GeneratedCVId =
                                    existingGeneratedCV
                                        .GeneratedCVId,

                                GeneratedCVText =
                                    generatedCVText,

                                GeneratedPdfFileName =
                                    pdfFileName,

                                GeneratedPdfFilePath =
                                    pdfFilePath,

                                GeneratedAt =
                                    generatedAt
                            },
                            transaction
                        );
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch
            {
                DeleteFileWithoutFailingRequest(
                    pdfFilePath,
                    "NEW GENERATED CV CLEANUP ERROR"
                );

                throw;
            }

            if (
                existingGeneratedCV != null &&
                !string.IsNullOrWhiteSpace(
                    existingGeneratedCV
                        .GeneratedPdfFilePath
                ) &&
                !string.Equals(
                    existingGeneratedCV
                        .GeneratedPdfFilePath,
                    pdfFilePath,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                DeleteFileWithoutFailingRequest(
                    existingGeneratedCV
                        .GeneratedPdfFilePath,
                    "OLD GENERATED CV DELETE ERROR"
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
            List<string> lines,
            string pdfFilePath)
        {
            Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);

                    page.Margin(35);

                    page.DefaultTextStyle(style =>
                        style.FontSize(10)
                    );

                    page.Header()
                        .PaddingBottom(8)
                        .BorderBottom(1)
                        .Text("Professional CV")
                        .SemiBold()
                        .FontSize(16);

                    page.Content()
                        .PaddingVertical(8)
                        .Column(column =>
                        {
                            column.Spacing(2);

                            foreach (string line in lines)
                            {
                                AddLineToPdf(
                                    column,
                                    line
                                );
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

        private static void AddLineToPdf(
            ColumnDescriptor column,
            string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                column.Item().Height(3);
                return;
            }

            if (IsSectionHeading(line))
            {
                column.Item()
                    .PaddingTop(6)
                    .PaddingBottom(2)
                    .Text(RemoveBulletPrefix(line))
                    .Bold()
                    .FontSize(12);

                return;
            }

            if (HasBulletPrefix(line))
            {
                string bulletText =
                    RemoveBulletPrefix(line);

                if (string.IsNullOrWhiteSpace(
                    bulletText))
                {
                    return;
                }

                column.Item()
                    .Row(row =>
                    {
                        row.ConstantItem(12)
                            .Text("•");

                        row.RelativeItem()
                            .Text(bulletText)
                            .LineHeight(1.2f);
                    });

                return;
            }

            column.Item()
                .Text(line)
                .LineHeight(1.2f);
        }

        private static List<string> PrepareCVLines(
            string generatedCVText)
        {
            string[] rawLines =
                generatedCVText
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n")
                    .Split('\n');

            var preparedLines =
                new List<string>();

            bool previousLineWasEmpty = false;

            foreach (string rawLine in rawLines)
            {
                string line =
                    CleanCVLine(rawLine);

                if (string.IsNullOrWhiteSpace(line))
                {
                    if (!previousLineWasEmpty &&
                        preparedLines.Count > 0)
                    {
                        preparedLines.Add(
                            string.Empty
                        );
                    }

                    previousLineWasEmpty = true;
                    continue;
                }

                if (IsEmptyBulletLine(line))
                {
                    continue;
                }

                previousLineWasEmpty = false;

                preparedLines.Add(line);
            }

            while (preparedLines.Count > 0 &&
                   string.IsNullOrWhiteSpace(
                       preparedLines[^1]))
            {
                preparedLines.RemoveAt(
                    preparedLines.Count - 1
                );
            }

            return preparedLines;
        }

        private static string CleanCVLine(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string cleanedValue =
                value.Trim();

            cleanedValue =
                Regex.Replace(
                    cleanedValue,
                    @"^#{1,6}\s*",
                    ""
                );

            cleanedValue =
                cleanedValue
                    .Replace("***", "")
                    .Replace("**", "")
                    .Replace("__", "");

            if (Regex.IsMatch(
                cleanedValue,
                @"^[-_*]{3,}$"))
            {
                return string.Empty;
            }

            cleanedValue =
                Regex.Replace(
                    cleanedValue,
                    @"[ \t]{2,}",
                    " "
                );

            return cleanedValue.Trim();
        }

        private static bool IsSectionHeading(
            string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            string value =
                RemoveBulletPrefix(line)
                    .Trim()
                    .TrimEnd(':')
                    .Trim();

            string[] knownHeadings =
            {
                "PROFESSIONAL SUMMARY",
                "SUMMARY",
                "PROFILE",
                "OBJECTIVE",
                "WORK EXPERIENCE",
                "PROFESSIONAL EXPERIENCE",
                "EXPERIENCE",
                "EDUCATION",
                "SKILLS",
                "TECHNICAL SKILLS",
                "PROJECTS",
                "CERTIFICATIONS",
                "LANGUAGES",
                "ACHIEVEMENTS",
                "REFERENCES"
            };

            if (knownHeadings.Contains(
                value,
                StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            return value.Length <= 45 &&
                   value.Any(char.IsLetter) &&
                   value ==
                       value.ToUpperInvariant();
        }

        private static bool HasBulletPrefix(
            string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            string value =
                line.TrimStart();

            return value.StartsWith("•") ||
                   value.StartsWith("▪") ||
                   value.StartsWith("◦") ||
                   value.StartsWith("- ") ||
                   value.StartsWith("* ") ||
                   value.StartsWith("– ") ||
                   value.StartsWith("— ");
        }

        private static bool IsEmptyBulletLine(
            string line)
        {
            string value =
                line.Trim();

            return value == "•" ||
                   value == "▪" ||
                   value == "◦" ||
                   value == "-" ||
                   value == "*" ||
                   value == "–" ||
                   value == "—";
        }

        private static string RemoveBulletPrefix(
            string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return string.Empty;

            string value =
                line.TrimStart();

            if (value.StartsWith("•") ||
                value.StartsWith("▪") ||
                value.StartsWith("◦") ||
                value.StartsWith("-") ||
                value.StartsWith("*") ||
                value.StartsWith("–") ||
                value.StartsWith("—"))
            {
                value =
                    value.Substring(1);
            }

            return value.Trim();
        }

        private static string CreateSafeFileName(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Job";

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
                ? "Job"
                : safeValue;
        }

        private static string BuildCVSkillsText(
            List<CVSkillData> skills)
        {
            if (skills.Count == 0)
            {
                return
                    "No extracted CV skills were found.";
            }

            return string.Join(
                Environment.NewLine,
                skills.Select(skill =>
                    $"- {skill.SkillName}: " +
                    $"{skill.YearsOfExperience ?? 0} years"
                )
            );
        }

        private static void DeleteFileWithoutFailingRequest(
            string? filePath,
            string logPrefix)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    $"{logPrefix}: {exception.Message}"
                );
            }
        }

        private class ExistingGeneratedCVData
        {
            public int GeneratedCVId { get; set; }

            public string? GeneratedPdfFileName
            {
                get;
                set;
            }

            public string? GeneratedPdfFilePath
            {
                get;
                set;
            }
        }

        private class ApplicationRefinementData
        {
            public int ApplicationId { get; set; }

            public int CVId { get; set; }

            public string OriginalCVText { get; set; }
                = string.Empty;

            public string JobTitle { get; set; }
                = string.Empty;

            public string CompanyName { get; set; }
                = string.Empty;

            public string JobDescription { get; set; }
                = string.Empty;
        }

        private class CVSkillData
        {
            public string SkillName { get; set; }
                = string.Empty;

            public decimal? YearsOfExperience { get; set; }
        }
    }
}