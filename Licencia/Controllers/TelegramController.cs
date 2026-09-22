using ClientAccess.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientAccess.Controllers
{
    [Route("Api/[controller]/secure")]
    [ApiController]
    public class TelegramController : ControllerBase
    {
        private readonly TelegramService _telegramService = new TelegramService();
        private readonly ConfigService _configService = new ConfigService();

        [HttpPost("test")]
        public async Task<IActionResult> Test()
        {
            await _telegramService.SendTestMessage();
            return Ok(new { success = true, message = "Mensaje de prueba enviado." });
        }

        [HttpPost("status")]
        public async Task<IActionResult> Status()
        {
            var config = await _configService.GetConfigAsync();
            var hasToken = !string.IsNullOrWhiteSpace(config.ContainsKey("TELEGRAM_BOT_TOKEN") ? config["TELEGRAM_BOT_TOKEN"] : "");
            var hasChatId = !string.IsNullOrWhiteSpace(config.ContainsKey("TELEGRAM_CHAT_ID") ? config["TELEGRAM_CHAT_ID"] : "");

            return Ok(new
            {
                success = true,
                configured = hasToken && hasChatId,
                hasToken,
                hasChatId
            });
        }
    }
}
