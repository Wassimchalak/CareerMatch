using CareerMatch.API.Data;
using CareerMatch.API.DTOs;
using CareerMatch.API.Models;
using Dapper;
using System.Security.Cryptography;
using System.Text;
using UglyToad.PdfPig;

namespace CareerMatch.API.Services
{
    public class CVService
    {
        private readonly DbConnectionFactory _dbConnectionFactory;
        private readonly IWebHostEnvironment _environment;
        private readonly AIService _aiService;

        public CVService(
            DbConnectionFactory dbConnectionFactory,
            IWebHostEnvironment environment,
            AIService aiService)
        {
            _dbConnectionFactory = dbConnectionFactory;
            _environment = environment;
            _aiService = aiService;
        }

        public async Task<CVResponse?> UploadCVAsync(
            int userId,
            IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }

            string extension =
                Path.GetExtension(file.FileName)
                    .ToLowerInvariant();

            if (extension != ".pdf")
            {
                return null;
            }

            string uploadsFolder =
                Path.Combine(
                    _environment.ContentRootPath,
                    "Uploads",
                    "CVs"
                );

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string storedFileName =
                $"{Guid.NewGuid()}{extension}";

            string filePath =
                Path.Combine(
                    uploadsFolder,
                    storedFileName
                );

            using (var stream =
                new FileStream(
                    filePath,
                    FileMode.Create
                ))
            {
                await file.CopyToAsync(stream);
            }

            string extractedText =
                ExtractTextFromPdf(filePath);

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                DeleteFileIfExists(filePath);

                throw new InvalidOperationException(
                    "Please upload a valid CV."
                );
            }

            string cvTextHash =
                CreateTextHash(extractedText);

            /*
             * AI now extracts:
             *
             * - PrimaryRole
             * - ExperienceLevel
             * - ProfessionalYearsOfExperience
             * - Skills
             * - YearsOfExperience for each skill
             */
            var aiResult =
                await _aiService.ExtractSkillsAsync(
                    extractedText
                );

            bool hasPrimaryRole =
                !string.IsNullOrWhiteSpace(
                    aiResult?.PrimaryRole
                );

            bool hasSkills =
                aiResult?.Skills != null &&
                aiResult.Skills.Any(skill =>
                    !string.IsNullOrWhiteSpace(
                        skill.SkillName
                    )
                );

            if (!hasPrimaryRole || !hasSkills)
            {
                DeleteFileIfExists(filePath);

                throw new InvalidOperationException(
                    "Please upload a valid CV."
                );
            }

            /*
             * Normalize the AI result before saving it.
             *
             * This prevents an unexpected AI value such as
             * "Junior" or "Mid-Level" from violating the
             * database constraint.
             */
            string experienceLevel =
                NormalizeExperienceLevel(
                    aiResult!.ExperienceLevel
                );

            decimal professionalYearsOfExperience =
                Math.Max(
                    0m,
                    aiResult.ProfessionalYearsOfExperience
                );

            var cv = new CV
            {
                UserId = userId,

                OriginalFileName =
                    file.FileName,

                StoredFileName =
                    storedFileName,

                FilePath =
                    filePath,

                ExtractedText =
                    extractedText,

                PrimaryRole =
                    aiResult.PrimaryRole.Trim(),

                ExperienceLevel =
                    experienceLevel,

                ProfessionalYearsOfExperience =
                    professionalYearsOfExperience,

                CVTextHash =
                    cvTextHash,

                UploadedAt =
                    DateTime.UtcNow
            };

            using var connection =
                _dbConnectionFactory.CreateConnection();

            connection.Open();

            using var transaction =
                connection.BeginTransaction();

            try
            {
                const string insertCvSql = @"
                    INSERT INTO CVs
                    (
                        UserId,
                        OriginalFileName,
                        StoredFileName,
                        FilePath,
                        ExtractedText,
                        PrimaryRole,
                        ExperienceLevel,
                        ProfessionalYearsOfExperience,
                        CVTextHash,
                        UploadedAt
                    )
                    OUTPUT INSERTED.CVId
                    VALUES
                    (
                        @UserId,
                        @OriginalFileName,
                        @StoredFileName,
                        @FilePath,
                        @ExtractedText,
                        @PrimaryRole,
                        @ExperienceLevel,
                        @ProfessionalYearsOfExperience,
                        @CVTextHash,
                        @UploadedAt
                    );
                ";

                cv.CVId =
                    await connection
                        .ExecuteScalarAsync<int>(
                            insertCvSql,
                            cv,
                            transaction
                        );

                /*
                 * The AI may accidentally return the same
                 * skill more than once.
                 *
                 * Keep one copy and preserve the highest
                 * supported experience value.
                 */
                var uniqueSkills =
                    aiResult.Skills
                        .Where(skill =>
                            !string.IsNullOrWhiteSpace(
                                skill.SkillName
                            )
                        )
                        .GroupBy(
                            skill =>
                                skill.SkillName.Trim(),

                            StringComparer.OrdinalIgnoreCase
                        )
                        .Select(group =>
                            group
                                .OrderByDescending(skill =>
                                    skill.YearsOfExperience
                                )
                                .First()
                        )
                        .ToList();

                foreach (var aiSkill in uniqueSkills)
                {
                    string skillName =
                        aiSkill.SkillName.Trim();

                    var skill =
                        await connection
                            .QueryFirstOrDefaultAsync<Skill>(
                                @"
                                SELECT
                                    SkillId,
                                    SkillName
                                FROM Skills
                                WHERE SkillName = @SkillName;
                                ",
                                new
                                {
                                    SkillName =
                                        skillName
                                },
                                transaction
                            );

                    if (skill == null)
                    {
                        int newSkillId =
                            await connection
                                .ExecuteScalarAsync<int>(
                                    @"
                                    INSERT INTO Skills
                                    (
                                        SkillName,
                                        CreatedAt
                                    )
                                    OUTPUT INSERTED.SkillId
                                    VALUES
                                    (
                                        @SkillName,
                                        @CreatedAt
                                    );
                                    ",
                                    new
                                    {
                                        SkillName =
                                            skillName,

                                        CreatedAt =
                                            DateTime.UtcNow
                                    },
                                    transaction
                                );

                        skill = new Skill
                        {
                            SkillId =
                                newSkillId,

                            SkillName =
                                skillName
                        };
                    }

                    /*
                     * Skill experience is separate from
                     * professional seniority.
                     *
                     * Example:
                     *
                     * ExperienceLevel = Entry
                     * ProfessionalYearsOfExperience = 0
                     *
                     * C# = 1
                     * React = 1
                     * SQL Server = 1
                     *
                     * This is valid when those skills are
                     * strongly demonstrated through projects,
                     * internships, or substantial practical use.
                     */
                    int skillYearsOfExperience =
                        Math.Max(
                            0,
                            aiSkill.YearsOfExperience
                        );

                    await connection.ExecuteAsync(
                        @"
                        INSERT INTO ExtractedCVSkills
                        (
                            CVId,
                            SkillId,
                            YearsOfExperience,
                            CreatedAt
                        )
                        VALUES
                        (
                            @CVId,
                            @SkillId,
                            @YearsOfExperience,
                            @CreatedAt
                        );
                        ",
                        new
                        {
                            CVId =
                                cv.CVId,

                            SkillId =
                                skill.SkillId,

                            YearsOfExperience =
                                skillYearsOfExperience,

                            CreatedAt =
                                DateTime.UtcNow
                        },
                        transaction
                    );
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();

                DeleteFileIfExists(filePath);

                throw;
            }

            return new CVResponse
            {
                CVId =
                    cv.CVId,

                UserId =
                    cv.UserId,

                OriginalFileName =
                    cv.OriginalFileName,

                StoredFileName =
                    cv.StoredFileName,

                FilePath =
                    cv.FilePath,

                UploadedAt =
                    cv.UploadedAt
            };
        }

        /*
         * Converts whatever the AI returns into one of
         * the values accepted by CareerMatch.
         *
         * Final stored values:
         *
         * Entry
         * Mid
         * Senior
         * Lead
         * Unknown
         */
        private static string NormalizeExperienceLevel(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Unknown";
            }

            string normalizedValue =
                value
                    .Trim()
                    .ToLowerInvariant();

            return normalizedValue switch
            {
                "entry" =>
                    "Entry",

                "entry level" =>
                    "Entry",

                "entry-level" =>
                    "Entry",

                "junior" =>
                    "Entry",

                "jr" =>
                    "Entry",

                "mid" =>
                    "Mid",

                "mid level" =>
                    "Mid",

                "mid-level" =>
                    "Mid",

                "intermediate" =>
                    "Mid",

                "senior" =>
                    "Senior",

                "sr" =>
                    "Senior",

                "lead" =>
                    "Lead",

                "technical lead" =>
                    "Lead",

                "tech lead" =>
                    "Lead",

                "principal" =>
                    "Lead",

                _ =>
                    "Unknown"
            };
        }

        private static string ExtractTextFromPdf(
            string filePath)
        {
            var text =
                new StringBuilder();

            using var document =
                PdfDocument.Open(filePath);

            foreach (var page in document.GetPages())
            {
                text.AppendLine(
                    page.Text
                );
            }

            return text.ToString();
        }

        private static string CreateTextHash(
            string? value)
        {
            string normalizedText =
                NormalizeText(value);

            byte[] textBytes =
                Encoding.UTF8.GetBytes(
                    normalizedText
                );

            byte[] hashBytes =
                SHA256.HashData(
                    textBytes
                );

            return Convert.ToHexString(
                hashBytes
            );
        }

        private static string NormalizeText(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return string.Join(
                    " ",
                    value.Split(
                        new[]
                        {
                            ' ',
                            '\r',
                            '\n',
                            '\t'
                        },
                        StringSplitOptions
                            .RemoveEmptyEntries
                    )
                )
                .Trim();
        }

        private static void DeleteFileIfExists(
            string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}