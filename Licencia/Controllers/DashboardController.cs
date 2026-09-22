using ClientAccess.Models;
using ClientAccess.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientAccess.Controllers
{
    [Route("Api/[controller]/secure")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly LeadService _leadService = new LeadService();
        private readonly AccessHelper _accessHelper = new AccessHelper();
        private readonly ClientAccessServices _clientAccessServices = new ClientAccessServices();

        [HttpPost("stats")]
        public async Task<IActionResult> GetStats([FromBody] AccessKeyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AccessKey))
                return BadRequest(new { success = false, message = "AccessKey requerida." });

            var validation = await _clientAccessServices.ActivateAccessAsync(new ActivateAccessRequest
            {
                AccessKey = request.AccessKey,
                AppCode = "att-bot"
            });

            if (validation.AccessStatus is "BLOCKED" or "INVALID")
                return Unauthorized(new { success = false, message = validation.Message });

            var accessId = validation.AccessId ?? 0;
            var stats = await _leadService.GetDashboardStatsAsync(accessId);

            return Ok(new { success = true, data = stats });
        }
    }

    public class AccessKeyRequest
    {
        public string AccessKey { get; set; } = "";
    }
}
