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
    public class SavedJobController : ControllerBase
    {
        private readonly SavedJobService _savedJobService;

        public SavedJobController(
            SavedJobService savedJobService)
        {
            _savedJobService = savedJobService;
        }

        [HttpPost("save")]
        public async Task<IActionResult> SaveJob(
            SavedJobRequest request)
        {
            if (request.JobId <= 0)
            {
                return BadRequest("JobId is required.");
            }

            int userId = GetAuthenticatedUserId();

            bool saved =
                await _savedJobService.SaveJobAsync(
                    userId,
                    request
                );

            if (!saved)
            {
                return NotFound("Job not found.");
            }

            return Ok("Job saved successfully.");
        }

        [HttpGet("mine")]
        public async Task<IActionResult> GetSavedJobs()
        {
            int userId = GetAuthenticatedUserId();

            var savedJobs =
                await _savedJobService.GetSavedJobsAsync(userId);

            return Ok(savedJobs);
        }

        [HttpPost("{jobId}/calculate-score")]
        public async Task<IActionResult> CalculateSavedJobScore(
            int jobId)
        {
            if (jobId <= 0)
            {
                return BadRequest("JobId is required.");
            }

            int userId = GetAuthenticatedUserId();

            var result =
                await _savedJobService.CalculateSavedJobScoreAsync(
                    userId,
                    jobId
                );

            if (result == null)
            {
                return NotFound(
                    "Saved job was not found or its score could not be calculated."
                );
            }

            return Ok(result);
        }

        [HttpDelete("{jobId}")]
        public async Task<IActionResult> UnsaveJob(int jobId)
        {
            if (jobId <= 0)
            {
                return BadRequest("JobId is required.");
            }

            int userId = GetAuthenticatedUserId();

            bool removed =
                await _savedJobService.UnsaveJobAsync(
                    userId,
                    jobId
                );

            if (!removed)
            {
                return NotFound("Saved job not found.");
            }

            return Ok("Job removed from saved jobs.");
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