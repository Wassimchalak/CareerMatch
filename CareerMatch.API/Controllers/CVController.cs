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
    public class CVController : ControllerBase
    {
        private readonly CVService _cvService;

        public CVController(CVService cvService)
        {
            _cvService = cvService;
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadCV(
            [FromForm] CVUploadRequest request)
        {
            try
            {
                int userId = GetAuthenticatedUserId();

                var result =
                    await _cvService.UploadCVAsync(
                        userId,
                        request.File
                    );

                if (result == null)
                {
                    return BadRequest(
                        "Invalid file. Please upload a valid PDF."
                    );
                }

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
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