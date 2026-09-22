using ClientAccess.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientAccess.Controllers
{
    [Route("Api/[controller]/secure")]
    [ApiController]
    public class ConfigController : ControllerBase
    {
        private readonly ConfigService _configService = new ConfigService();

        [HttpPost("get")]
        public async Task<IActionResult> GetConfig()
        {
            var config = await _configService.GetConfigAsync();
            return Ok(new { success = true, data = config });
        }

        [HttpPost("save")]
        public async Task<IActionResult> SaveConfig([FromBody] SaveConfigRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Key) || request.Value == null)
                return BadRequest(new { success = false, message = "Key y Value requeridos." });

            await _configService.SaveConfigAsync(request.Key, request.Value);
            return Ok(new { success = true });
        }

        [HttpPost("save-batch")]
        public async Task<IActionResult> SaveConfigBatch([FromBody] Dictionary<string, string> config)
        {
            await _configService.SaveConfigBatchAsync(config);
            return Ok(new { success = true });
        }
    }

    public class SaveConfigRequest
    {
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
    }
}
