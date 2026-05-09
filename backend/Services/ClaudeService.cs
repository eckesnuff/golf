using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Models.Messages;
using backend.Models;
using Microsoft.Extensions.Configuration;

namespace backend.Services
{
    public class ClaudeService
    {
        private readonly AnthropicClient _client;
        private const string Model = "claude-opus-4-7";

        public ClaudeService(IConfiguration config)
        {
            _client = new AnthropicClient { ApiKey = config["Anthropic:ApiKey"] };
        }

        public async Task<ClaudeReply> AskAsync(string systemContext, List<ClaudeMessage> history)
        {
            var messages = history.Select(m => new MessageParam
            {
                Role = m.Role == "user" ? Role.User : Role.Assistant,
                Content = m.Content
            }).ToList();

            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = Model,
                MaxTokens = 2048,
                System = new List<TextBlockParam>
                {
                    new() { Text = systemContext, CacheControl = new CacheControlEphemeral() }
                },
                Messages = messages
            });

            return new ClaudeReply
            {
                Text = response.Content.Select(b => b.Value).OfType<TextBlock>().First().Text,
                Usage = new ClaudeUsage
                {
                    InputTokens = response.Usage.InputTokens,
                    OutputTokens = response.Usage.OutputTokens,
                    CacheCreationTokens = response.Usage.CacheCreationInputTokens,
                    CacheReadTokens = response.Usage.CacheReadInputTokens
                }
            };
        }
    }
}
