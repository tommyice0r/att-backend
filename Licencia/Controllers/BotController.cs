using ClientAccess.Models;
using ClientAccess.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientAccess.Controllers
{
    [Route("Api/[controller]/secure")]
    [ApiController]
    public class BotController : ControllerBase
    {
        private readonly ClientAccessServices _clientAccessServices = new ClientAccessServices();
        private readonly LeadService _leadService = new LeadService();
        private readonly HitService _hitService = new HitService();

        private async Task<int?> ValidateAndGetAccessId(string accessKey)
        {
            var validation = await _clientAccessServices.ActivateAccessAsync(new ActivateAccessRequest
            {
                AccessKey = accessKey,
                AppCode = "att-bot"
            });

            if (validation.AccessStatus is "BLOCKED" or "INVALID")
                return null;

            return validation.AccessId;
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartBot([FromBody] AccessKeyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AccessKey))
                return BadRequest(new { success = false, message = "AccessKey requerida." });

            var accessId = await ValidateAndGetAccessId(request.AccessKey);
            if (accessId == null)
                return Unauthorized(new { success = false, message = "Acceso bloqueado o inválido." });

            // Aquí se dispararía el bot (Node.js) vía un proceso o señal
            // Por ahora solo registramos la intención
            return Ok(new { success = true, message = "Bot iniciado." });
        }

        [HttpPost("stats")]
        public async Task<IActionResult> GetStats([FromBody] AccessKeyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AccessKey))
                return BadRequest(new { success = false, message = "AccessKey requerida." });

            var accessId = await ValidateAndGetAccessId(request.AccessKey);
            if (accessId == null)
                return Unauthorized(new { success = false, message = "Acceso bloqueado o inválido." });

            var stats = await _leadService.GetDashboardStatsAsync(accessId.Value);
            return Ok(new { success = true, data = stats });
        }
    }
}
