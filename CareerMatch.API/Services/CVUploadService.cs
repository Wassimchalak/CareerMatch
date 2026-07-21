using CareerMatch.API.Data;
using CareerMatch.API.DTOs;
using CareerMatch.API.Models;
using Dapper;
using System.Security.Cryptography;
using System.Text;
using UglyToad.PdfPig;

namespace CareerMatch.API.Services
{
    // Handles CV upload, PDF text extraction, CV text hashing,
    // OpenAI skill extraction, and database persistence.
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
            // Reject missing or empty files.
            if (file == null || file.Length == 0)
                return null;

            // Only PDF files are accepted.
            string extension =
                Path.GetExtension(file.FileName)
                    .ToLowerInvariant();

            if (extension != ".pdf")
                return null;

            // Create the CV uploads folder when necessary.
            string uploadsFolder =
                Path.Combine(
                    _environment.ContentRootPath,
                    "Uploads",
                    "CVs"
                );

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(
                    uploadsFolder
                );
            }

            // Generate a unique server-side filename.
            string storedFileName =
                $"{Guid.NewGuid()}{extension}";

            string filePath =
                Path.Combine(
                    uploadsFolder,
                    storedFileName
                );

            // Save the PDF to disk.
            using (var stream =
                new FileStream(
                    filePath,
                    FileMode.Create
                ))
            {
                await file.CopyToAsync(stream);
            }

            // Extract text from all PDF pages.
            string extractedText =
                ExtractTextFromPdf(filePath);

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                File.Delete(filePath);
                return null;
            }

            // Generate a stable SHA-256 hash from normalized CV text.
            // MatchingService later uses this value to validate cache entries.
            string cvTextHash =
                CreateTextHash(extractedText);

            // Extract the primary role and skills using OpenAI.
            var aiResult =
                await _aiService.ExtractSkillsAsync(
                    extractedText
                );

            // Build the CV database model.
            var cv = new CV
            {
                UserId = userId,
                OriginalFileName = file.FileName,
                StoredFileName = storedFileName,
                FilePath = filePath,
                ExtractedText = extractedText,
                PrimaryRole = aiResult.PrimaryRole,
                CVTextHash = cvTextHash,
                UploadedAt = DateTime.Now
            };

            using var connection =
                _dbConnectionFactory.CreateConnection();

            // Keep the CV and extracted-skill inserts atomic.
            connection.Open();

            using var transaction =
                connection.BeginTransaction();

            try
            {
                // Insert the CV, including CVTextHash.
                string insertCvSql = @"
                    INSERT INTO CVs
                    (
                        UserId,
                        OriginalFileName,
                        StoredFileName,
                        FilePath,
                        ExtractedText,
                        PrimaryRole,
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
                        @CVTextHash,
                        @UploadedAt
                    );
                ";

                cv.CVId =
                    await connection.ExecuteScalarAsync<int>(
                        insertCvSql,
                        cv,
                        transaction
                    );

                // Remove empty and duplicate skill names before insertion.
                var uniqueSkills =
                    (aiResult.Skills
                        ?? new List<AIExtractedSkill>())
                    .Where(skill =>
                        !string.IsNullOrWhiteSpace(
                            skill.SkillName
                        )
                    )
                    .GroupBy(
                        skill => skill.SkillName.Trim(),
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

                    // Reuse an existing normalized skill when possible.
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
                                    SkillName = skillName
                                },
                                transaction
                            );

                    // Insert the skill only once in the Skills table.
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
                                        SkillName = skillName,
                                        CreatedAt =
                                            DateTime.Now
                                    },
                                    transaction
                                );

                        skill = new Skill
                        {
                            SkillId = newSkillId,
                            SkillName = skillName
                        };
                    }

                    // Link the extracted skill to this CV.
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
                            CVId = cv.CVId,
                            SkillId = skill.SkillId,
                            YearsOfExperience =
                                Math.Max(
                                    0,
                                    aiSkill.YearsOfExperience
                                ),
                            CreatedAt = DateTime.Now
                        },
                        transaction
                    );
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();

                // Avoid leaving an uploaded file when the database save fails.
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                throw;
            }

            return new CVResponse
            {
                CVId = cv.CVId,
                UserId = cv.UserId,
                OriginalFileName =
                    cv.OriginalFileName,
                StoredFileName =
                    cv.StoredFileName,
                FilePath = cv.FilePath,
                UploadedAt = cv.UploadedAt
            };
        }

        // Extracts text from every PDF page.
        private static string ExtractTextFromPdf(
            string filePath)
        {
            var text = new StringBuilder();

            using var document =
                PdfDocument.Open(filePath);

            foreach (var page in document.GetPages())
            {
                text.AppendLine(page.Text);
            }

            return text.ToString();
        }

        // Creates a 64-character SHA-256 hexadecimal hash.
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

        // Removes differences caused only by extra spaces,
        // line breaks, or tabs before hashing.
        private static string NormalizeText(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

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
                    StringSplitOptions.RemoveEmptyEntries
                )
            )
            .Trim();
        }
    }
}