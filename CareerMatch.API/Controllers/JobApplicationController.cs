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
    public class JobApplicationController : ControllerBase
    {
        private readonly JobApplicationService
            _jobApplicationService;

        public JobApplicationController(
            JobApplicationService jobApplicationService)
        {
            _jobApplicationService =
                jobApplicationService;
        }

        [HttpPost("apply")]
        public async Task<IActionResult> ApplyForJob(
            JobApplicationRequest request)
        {
            if (request.JobId <= 0)
            {
                return BadRequest(
                    "JobId is required."
                );
            }

            int userId =
                GetAuthenticatedUserId();

            var result =
                await _jobApplicationService
                    .ApplyForJobAsync(
                        userId,
                        request
                    );

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpGet("mine")]
        public async Task<IActionResult>
            GetMyApplications()
        {
            int userId =
                GetAuthenticatedUserId();

            var applications =
                await _jobApplicationService
                    .GetUserApplicationsAsync(
                        userId
                    );

            return Ok(applications);
        }

        // This method must be inside JobApplicationController.
        [HttpDelete("{applicationId:int}")]
        public async Task<IActionResult>
            DeleteApplication(
                int applicationId)
        {
            if (applicationId <= 0)
            {
                return BadRequest(
                    new
                    {
                        message =
                            "ApplicationId is required."
                    }
                );
            }

            int userId =
                GetAuthenticatedUserId();

            bool deleted =
                await _jobApplicationService
                    .DeleteApplicationAsync(
                        userId,
                        applicationId
                    );

            if (!deleted)
            {
                return NotFound(
                    new
                    {
                        message =
                            "Application not found."
                    }
                );
            }

            return Ok(
                new
                {
                    success = true,
                    message =
                        "Application removed successfully."
                }
            );
        }

        private int GetAuthenticatedUserId()
        {
            string? value =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                );

            if (!int.TryParse(
                    value,
                    out int userId
                ))
            {
                throw new UnauthorizedAccessException(
                    "Authenticated UserId is missing."
                );
            }

            return userId;
        }
    }
}