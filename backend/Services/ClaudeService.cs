using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using backend.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace backend.Services
{
    public class ClaudeService
    {
        private readonly HttpClient _http = new();
        private readonly string _apiKey;
        private const string ApiUrl = "https://api.anthropic.com/v1/messages";
        private const string Model = "claude-opus-4-7";

        public ClaudeService(IConfiguration config)
        {
            _apiKey = config["Anthropic:ApiKey"];
        }

        public async Task<string> AskAsync(string systemContext, List<ClaudeMessage> history)
        {
            var messages = new List<object>();
            foreach (var msg in history)
                messages.Add(new { role = msg.Role, content = msg.Content });

            var body = JsonConvert.SerializeObject(new
            {
                model = Model,
                max_tokens = 2048,
                system = new[]
                {
                    new
                    {
                        type = "text",
                        text = systemContext,
                        cache_control = new { type = "ephemeral" }
                    }
                },
                messages
            });

            var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Headers.Add("anthropic-beta", "prompt-caching-2024-07-31");

            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Claude API error {response.StatusCode}: {json}");

            dynamic result = JsonConvert.DeserializeObject(json);
            return (string)result.content[0].text;
        }
    }
}
