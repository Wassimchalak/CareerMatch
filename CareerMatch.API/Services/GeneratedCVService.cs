using CareerMatch.API.Data;
using CareerMatch.API.DTOs;
using Dapper;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.RegularExpressions;

namespace CareerMatch.API.Services
{
    // Improves one application CV and returns its PDF in one operation.
    public class GeneratedCVService
    {
        // Creates SQL Server connections for Dapper.
        private readonly DbConnectionFactory _dbConnectionFactory;

        // Calls OpenAI.
        private readonly AIService _aiService;

        // Provides the application root folder.
        private readonly IWebHostEnvironment _environment;

        // Receives all required dependencies.
        public GeneratedCVService(
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

        // Generates, saves, converts, and returns one improved-CV PDF.
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
                    ApplicationRefinementData>(
                    @"
                    SELECT
                        ja.ApplicationId,
                        ja.CVId,
                        cv.ExtractedText AS OriginalCVText,
                        j.Title AS JobTitle,
                        j.CompanyName,
                        j.Description AS JobDescription,
                        matchedJob.Recommendation
                    FROM JobApplications ja
                    INNER JOIN CVs cv
                        ON ja.CVId = cv.CVId
                    INNER JOIN Jobs j
                        ON ja.JobId = j.JobId
                    OUTER APPLY
                    (
                        SELECT TOP 1
                            jm.Recommendation
                        FROM JobMatches jm
                        WHERE jm.UserId = ja.UserId
                          AND jm.CVId = ja.CVId
                          AND jm.JobId = ja.JobId
                          AND jm.CVTextHash = cv.CVTextHash
                          AND jm.DescriptionHash = j.DescriptionHash
                        ORDER BY jm.CreatedAt DESC,
                                 jm.JobMatchId DESC
                    ) matchedJob
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

            // Prevents generation from unreadable CV text.
            if (string.IsNullOrWhiteSpace(
                applicationData.OriginalCVText))
            {
                throw new Exception(
                    "The original CV contains no readable text."
                );
            }

            // A current match must exist so its saved recommendation can guide
            // refinement without making another recommendation request.
            if (string.IsNullOrWhiteSpace(
                applicationData.Recommendation))
            {
                throw new InvalidOperationException(
                    "No current job-match recommendation was found. " +
                    "Calculate the match for this CV and job before refining the CV."
                );
            }

            // Loads extracted skills from the exact application CV.
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
                            // Uses the exact application CV.
                            CVId = applicationData.CVId
                        }
                    )
                ).ToList();

            // Converts the candidate skills into prompt text.
            string cvSkillsText =
                BuildCVSkillsText(cvSkills);

            // Generates the improved CV.
            string generatedCVText =
                await _aiService.RefineCVForJobAsync(
                    applicationData.OriginalCVText,
                    cvSkillsText,
                    applicationData.JobTitle,
                    applicationData.CompanyName,
                    applicationData.JobDescription,
                    applicationData.Recommendation
                );

            // Rejects empty AI output.
            if (string.IsNullOrWhiteSpace(
                generatedCVText))
            {
                throw new Exception(
                    "OpenAI returned an empty refined CV."
                );
            }

            // Uses UTC so database timestamps are consistent
            // across local development and deployment environments.
            DateTime generatedAt = DateTime.UtcNow;

            // Creates the PDF folder.
            string pdfFolder =
                Path.Combine(
                    _environment.ContentRootPath,
                    "Uploads",
                    "GeneratedCVs"
                );

            // Ensures the folder exists.
            Directory.CreateDirectory(pdfFolder);

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

            // Creates a unique filename for the replacement PDF.
            // The old PDF is not overwritten immediately because the new
            // PDF must be generated successfully before the database row
            // and physical file are replaced.
            string pdfFileName =
                $"Refined_CV_{safeJobTitle}_{safeCompanyName}_{Guid.NewGuid():N}.pdf";

            // Builds the complete server path.
            string pdfFilePath =
                Path.Combine(
                    pdfFolder,
                    pdfFileName
                );

            // Cleans the generated text for PDF rendering.
            List<string> lines =
                PrepareCVLines(
                    generatedCVText
                );

            // Creates the new PDF before changing the database.
            CreatePdf(
                lines,
                pdfFilePath
            );

            ExistingGeneratedCVData?
                existingGeneratedCV = null;

            try
            {
                // Dapper may close a connection after an earlier command
                // when it originally opened that connection automatically.
                // A transaction requires the connection to be open.
                if (connection.State !=
                    System.Data.ConnectionState.Open)
                {
                    connection.Open();
                }

                using var transaction =
                    connection.BeginTransaction();

                try
                {
                    // Locks this application's generated-CV row while the
                    // insert-or-update decision is being made.
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
                        // This is the first refined CV for the application.
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
                        // A refined CV already exists for this application.
                        // Keep the same GeneratedCVId and replace its content.
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
                // The new PDF is not referenced by the database when the
                // database operation fails, so remove the orphaned file.
                DeleteFileWithoutFailingRequest(
                    pdfFilePath,
                    "NEW GENERATED CV CLEANUP ERROR"
                );

                throw;
            }

            // Delete the previous physical PDF only after the replacement
            // row was successfully committed to the database.
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

        // Creates the improved-CV PDF.
        private static void CreatePdf(
            List<string> lines,
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
                    page.Margin(35);

                    // Sets default typography.
                    page.DefaultTextStyle(style =>
                        style.FontSize(10)
                    );

                    // Adds the header.
                    page.Header()
                        .PaddingBottom(8)
                        .BorderBottom(1)
                        .Text("Professional CV")
                        .SemiBold()
                        .FontSize(16);

                    // Adds every prepared line.
                    page.Content()
                        .PaddingVertical(8)
                        .Column(column =>
                        {
                            // Adds small spacing between lines.
                            column.Spacing(2);

                            // Renders every line.
                            foreach (string line in lines)
                            {
                                AddLineToPdf(
                                    column,
                                    line
                                );
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
            // Writes the file.
            .GeneratePdf(pdfFilePath);
        }

        // Renders one prepared CV line.
        private static void AddLineToPdf(
            ColumnDescriptor column,
            string line)
        {
            // Adds spacing for blank lines.
            if (string.IsNullOrWhiteSpace(line))
            {
                column.Item().Height(3);
                return;
            }

            // Styles known or uppercase section headings.
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

            // Styles bullet lines.
            if (HasBulletPrefix(line))
            {
                // Removes the original bullet marker.
                string bulletText =
                    RemoveBulletPrefix(line);

                // Skips an empty bullet.
                if (string.IsNullOrWhiteSpace(
                    bulletText))
                {
                    return;
                }

                // Draws a bullet and its text.
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

            // Draws a normal paragraph line.
            column.Item()
                .Text(line)
                .LineHeight(1.2f);
        }

        // Cleans raw AI text into PDF-ready lines.
        private static List<string> PrepareCVLines(
            string generatedCVText)
        {
            // Normalizes line endings.
            string[] rawLines =
                generatedCVText
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n")
                    .Split('\n');

            // Stores cleaned lines.
            var preparedLines =
                new List<string>();

            // Prevents repeated empty lines.
            bool previousLineWasEmpty = false;

            // Processes each raw line.
            foreach (string rawLine in rawLines)
            {
                // Cleans markdown and extra spaces.
                string line =
                    CleanCVLine(rawLine);

                // Handles blank lines.
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

                // Skips bullet markers without content.
                if (IsEmptyBulletLine(line))
                {
                    continue;
                }

                // Records that this line contains text.
                previousLineWasEmpty = false;

                // Adds the cleaned line.
                preparedLines.Add(line);
            }

            // Removes trailing empty lines.
            while (preparedLines.Count > 0 &&
                   string.IsNullOrWhiteSpace(
                       preparedLines[^1]))
            {
                preparedLines.RemoveAt(
                    preparedLines.Count - 1
                );
            }

            // Returns the final lines.
            return preparedLines;
        }

        // Removes simple markdown artifacts.
        private static string CleanCVLine(
            string value)
        {
            // Returns empty for whitespace-only input.
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            // Trims the line.
            string cleanedValue =
                value.Trim();

            // Removes markdown heading symbols.
            cleanedValue =
                Regex.Replace(
                    cleanedValue,
                    @"^#{1,6}\s*",
                    ""
                );

            // Removes markdown emphasis markers.
            cleanedValue =
                cleanedValue
                    .Replace("***", "")
                    .Replace("**", "")
                    .Replace("__", "");

            // Removes markdown horizontal rules.
            if (Regex.IsMatch(
                cleanedValue,
                @"^[-_*]{3,}$"))
            {
                return string.Empty;
            }

            // Collapses repeated spaces and tabs.
            cleanedValue =
                Regex.Replace(
                    cleanedValue,
                    @"[ \t]{2,}",
                    " "
                );

            // Returns the final line.
            return cleanedValue.Trim();
        }

        // Detects likely CV section headings.
        private static bool IsSectionHeading(
            string line)
        {
            // Rejects empty input.
            if (string.IsNullOrWhiteSpace(line))
                return false;

            // Removes bullets and trailing punctuation.
            string value =
                RemoveBulletPrefix(line)
                    .Trim()
                    .TrimEnd(':')
                    .Trim();

            // Lists common CV headings.
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

            // Returns true for a known heading.
            if (knownHeadings.Contains(
                value,
                StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            // Uses a short-all-uppercase fallback rule.
            return value.Length <= 45 &&
                   value.Any(char.IsLetter) &&
                   value ==
                       value.ToUpperInvariant();
        }

        // Detects common bullet prefixes.
        private static bool HasBulletPrefix(
            string line)
        {
            // Rejects empty input.
            if (string.IsNullOrWhiteSpace(line))
                return false;

            // Ignores leading spaces.
            string value =
                line.TrimStart();

            // Checks supported bullet forms.
            return value.StartsWith("•") ||
                   value.StartsWith("▪") ||
                   value.StartsWith("◦") ||
                   value.StartsWith("- ") ||
                   value.StartsWith("* ") ||
                   value.StartsWith("– ") ||
                   value.StartsWith("— ");
        }

        // Detects bullet characters with no text.
        private static bool IsEmptyBulletLine(
            string line)
        {
            // Trims the line.
            string value =
                line.Trim();

            // Checks empty bullet forms.
            return value == "•" ||
                   value == "▪" ||
                   value == "◦" ||
                   value == "-" ||
                   value == "*" ||
                   value == "–" ||
                   value == "—";
        }

        // Removes one leading bullet marker.
        private static string RemoveBulletPrefix(
            string line)
        {
            // Returns empty for empty input.
            if (string.IsNullOrWhiteSpace(line))
                return string.Empty;

            // Trims leading spaces.
            string value =
                line.TrimStart();

            // Removes one supported bullet marker.
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

            // Returns the remaining text.
            return value.Trim();
        }

        // Creates a safe filename segment.
        private static string CreateSafeFileName(
            string value)
        {
            // Provides a fallback.
            if (string.IsNullOrWhiteSpace(value))
                return "Job";

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
                ? "Job"
                : safeValue;
        }

        // Converts CV skills into prompt text.
        private static string BuildCVSkillsText(
            List<CVSkillData> skills)
        {
            // Provides a clear fallback.
            if (skills.Count == 0)
            {
                return
                    "No extracted CV skills were found.";
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

        // Deletes a generated file without turning a successful
        // generation into a failed API request when cleanup alone fails.
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

        // Holds the current generated-CV row for one application.
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

        // Holds application, CV, and job query data.
        private class ApplicationRefinementData
        {
            // Stores the application id.
            public int ApplicationId { get; set; }

            // Stores the exact CV id.
            public int CVId { get; set; }

            // Stores extracted original CV text.
            public string OriginalCVText { get; set; }
                = string.Empty;

            // Stores the target job title.
            public string JobTitle { get; set; }
                = string.Empty;

            // Stores the company name.
            public string CompanyName { get; set; }
                = string.Empty;

            // Stores the full job description.
            public string JobDescription { get; set; }
                = string.Empty;

            // Stores the recommendation generated by the current job match.
            public string Recommendation { get; set; }
                = string.Empty;
        }

        // Holds one extracted CV skill row.
        private class CVSkillData
        {
            // Stores the skill name.
            public string SkillName { get; set; }
                = string.Empty;

            // Stores known or unknown years.
            public decimal? YearsOfExperience { get; set; }
        }
    }
}