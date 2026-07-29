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
    public class JobSearchController : ControllerBase
    {
        private readonly JobSearchService _jobSearchService;

        public JobSearchController(
            JobSearchService jobSearchService)
        {
            _jobSearchService = jobSearchService;
        }

        [HttpPost("search")]
        public async Task<ActionResult<List<JobSearchResponse>>>
            SearchJobs(
                [FromBody] JobSearchRequest request)
        {
            var jobs =
                await _jobSearchService
                    .SearchJobsAsync(request);

            return Ok(jobs);
        }

        [HttpPost("calculate-matches")]
        public async Task<ActionResult<List<JobSearchResponse>>>
            CalculateMatches(
                [FromBody] CalculateMatchesRequest request)
        {
            int userId = GetAuthenticatedUserId();

            try
            {
                var matchedJobs =
                    await _jobSearchService.CalculateMatchesAsync(
                        userId,
                        request
                    );

                return Ok(matchedJobs);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
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