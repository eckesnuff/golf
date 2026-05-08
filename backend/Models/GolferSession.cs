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
}
