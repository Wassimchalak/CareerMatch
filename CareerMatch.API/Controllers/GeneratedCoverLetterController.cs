using CareerMatch.API.DTOs;
using CareerMatch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CareerMatch.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class GeneratedCoverLetterController : ControllerBase
    {
        private readonly GeneratedCoverLetterService
            _generatedCoverLetterService;

        public GeneratedCoverLetterController(
            GeneratedCoverLetterService service)
        {
            _generatedCoverLetterService = service;
        }

        [HttpPost("generate")]
        public async Task<IActionResult> Generate(
            GenerateCoverLetterRequest request)
        {
            if (request.ApplicationId <= 0)
            {
                return BadRequest(
                    "ApplicationId is required."
                );
            }

            int userId = GetAuthenticatedUserId();

            GeneratedDocumentDownloadResult? result =
                await _generatedCoverLetterService
                    .GenerateAndDownloadForApplicationAsync(
                        userId,
                        request.ApplicationId
                    );

            if (result == null)
            {
                return NotFound(
                    "Job application not found."
                );
            }

            return File(
                result.FileBytes,
                result.ContentType,
                result.FileName
            );
        }

        private int GetAuthenticatedUserId()
        {
            string? value =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                );

            if (!int.TryParse(value, out int userId))
            {
                throw new UnauthorizedAccessException(
                    "Authenticated UserId is missing."
                );
            }

            return userId;
        }
    }
}
