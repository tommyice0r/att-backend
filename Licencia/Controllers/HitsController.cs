using ClientAccess.Models;
using ClientAccess.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientAccess.Controllers
{
    [Route("Api/[controller]/secure")]
    [ApiController]
    public class HitsController : ControllerBase
    {
        private readonly HitService _hitService = new HitService();
        private readonly ClientAccessServices _clientAccessServices = new ClientAccessServices();
        private readonly TelegramService _telegramService = new TelegramService();

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

        [HttpPost("save")]
        public async Task<IActionResult> SaveHit([FromBody] HitSaveRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AccessKey))
                return BadRequest(new { success = false, message = "AccessKey requerida." });

            var accessId = await ValidateAndGetAccessId(request.AccessKey);
            if (accessId == null)
                return Unauthorized(new { success = false, message = "Acceso bloqueado o inválido." });

            var hitId = await _hitService.SaveHitAsync(request, accessId.Value);

            // Telegram notification (fire & forget)
            var validation = await _clientAccessServices.ActivateAccessAsync(new ActivateAccessRequest
            {
                AccessKey = request.AccessKey,
                AppCode = "att-bot"
            });
            _ = _telegramService.SendHitNotification(
                request.PhoneNumber,
                request.FullName,
                request.DeviceMessage,
                request.HitType,
                request.ZipCode,
                validation?.ClientName
            );

            return Ok(new { success = true, hitId });
        }

        [HttpPost("list")]
        public async Task<IActionResult> GetHits([FromBody] AccessKeyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AccessKey))
                return BadRequest(new { success = false, message = "AccessKey requerida." });

            var accessId = await ValidateAndGetAccessId(request.AccessKey);
            if (accessId == null)
                return Unauthorized(new { success = false, message = "Acceso bloqueado o inválido." });

            var hits = await _hitService.GetHitsAsync(accessId.Value);
            return Ok(new { success = true, data = hits });
        }

        [HttpPost("count")]
        public async Task<IActionResult> CountHits([FromBody] AccessKeyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AccessKey))
                return BadRequest(new { success = false, message = "AccessKey requerida." });

            var accessId = await ValidateAndGetAccessId(request.AccessKey);
            if (accessId == null)
                return Unauthorized(new { success = false, message = "Acceso bloqueado o inválido." });

            var count = await _hitService.CountHitsAsync(accessId.Value);
            return Ok(new { success = true, count });
        }
    }
}
