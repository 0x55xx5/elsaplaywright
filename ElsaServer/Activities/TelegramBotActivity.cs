using System.Text;
using System.Text.Json;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;

namespace ElsaServer.Activities
{
    [Activity("TelegramBot", "Communication", "Sends a text message using a Telegram Bot via HTTP.")]
    public class TelegramBotActivity : CodeActivity
    {
        [Input(Description = "The Telegram Bot Token.")]
        public Input<string> Token { get; set; } = default!;

        [Input(Description = "The User ID or Chat ID to send the message to.")]
        public Input<string> UserId { get; set; } = default!;

        [Input(Description = "The text message content to send.")]
        public Input<string> Message { get; set; } = default!;

        [Output(Description = "The response body from the Telegram API.")]
        public Output<string> ResponseContent { get; set; } = default!;

        [Output(Description = "Indicates whether the message was sent successfully.")]
        public Output<bool> IsSuccess { get; set; } = default!;

        protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
        {
            var token = Token.Get(context);
            var userId = UserId.Get(context);
            var message = Message.Get(context);

            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(message))
            {
                IsSuccess.Set(context, false);
                ResponseContent.Set(context, "Token, UserId, and Message are required.");
                await context.CompleteActivityAsync();
                return;
            }

            var url = $"https://api.telegram.org/bot{token}/sendMessage";
            
            var payload = new
            {
                chat_id = userId,
                text = message
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            // Getting IHttpClientFactory if registered, otherwise using a new HttpClient
            var httpClientFactory = context.GetService<IHttpClientFactory>();
            using var httpClient = httpClientFactory != null ? httpClientFactory.CreateClient() : new HttpClient();

            try
            {
                var response = await httpClient.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                IsSuccess.Set(context, response.IsSuccessStatusCode);
                ResponseContent.Set(context, responseBody);
            }
            catch (Exception ex)
            {
                IsSuccess.Set(context, false);
                ResponseContent.Set(context, ex.Message);
            }

            await context.CompleteActivityAsync();
        }
    }
}
