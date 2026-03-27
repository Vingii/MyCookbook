using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyCookbook.Services;

namespace MyCookbook.Api
{
    [ApiController]
    [Route("api/feedback")]
    [Authorize(Policy = "CookieOrApiKey")]
    public class FeedbackController : ControllerBase
    {
        private readonly IFeedbackProvider _feedbackProvider;

        public FeedbackController(IFeedbackProvider feedbackProvider)
        {
            _feedbackProvider = feedbackProvider;
        }

        [HttpPost]
        [RequestSizeLimit(50 * 1024 * 1024)]
        public async Task<IActionResult> PostFeedback([FromForm] string message, [FromForm] IReadOnlyList<IFormFile>? files)
        {
            if (string.IsNullOrWhiteSpace(message))
                return BadRequest("Message is required.");

            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value
                          ?? User.Identity?.Name ?? "";
            await _feedbackProvider.ProvideFeedback(message, files, userName);
            return Ok();
        }
    }
}
