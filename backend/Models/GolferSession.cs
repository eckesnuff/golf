using System.Collections.Generic;

namespace backend.Models
{
    public class GolferSession
    {
        public string SystemContext { get; set; }
        public List<ClaudeMessage> History { get; set; } = new();
    }

    public class ClaudeMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }

    public class ClaudeReply
    {
        public string Text { get; set; }
        public ClaudeUsage Usage { get; set; }
    }

    public class ClaudeUsage
    {
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public long? CacheCreationTokens { get; set; }
        public long? CacheReadTokens { get; set; }
    }
}
