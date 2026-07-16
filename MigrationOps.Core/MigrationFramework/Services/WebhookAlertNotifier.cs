using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace MigrationOps.Core.MigrationFramework.Services
{
    public class WebhookAlertNotifier : IMigrationAlertNotifier
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        private readonly string? _webhookUrl;
        private readonly bool _enabled;

        public WebhookAlertNotifier(string? webhookUrl, bool enabled)
        {
            _webhookUrl = webhookUrl;
            _enabled = enabled;
        }

        public async Task NotifyFailureAsync(string migrationName, string database, string errorMessage)
        {
            if (!_enabled || string.IsNullOrWhiteSpace(_webhookUrl))
            {
                return;
            }

            try
            {
                var payload = new
                {
                    text = $"MigrationOps: {migrationName} failed on {database}: {errorMessage}",
                    migration = migrationName,
                    database,
                    error = errorMessage,
                    timestamp = DateTime.UtcNow.ToString("o")
                };

                using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                await HttpClient.PostAsync(_webhookUrl, content);
            }
            catch (Exception ex)
            {
                // A broken webhook must never fail a migration run.
                Console.WriteLine($"Failed to send failure alert webhook: {ex.Message}");
            }
        }
    }
}
