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
    public class GeneratedCVController : ControllerBase
    {
        private readonly GeneratedCVService
            _generatedCVService;

        public GeneratedCVController(
            GeneratedCVService service)
        {
            _generatedCVService = service;
        }

        [HttpPost("generate")]
        public async Task<IActionResult> Generate(
            GenerateCVRequest request)
        {
            if (request.ApplicationId <= 0)
            {
                return BadRequest(
                    "ApplicationId is required."
                );
            }

            int userId = GetAuthenticatedUserId();

            GeneratedDocumentDownloadResult? result =
                await _generatedCVService
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
