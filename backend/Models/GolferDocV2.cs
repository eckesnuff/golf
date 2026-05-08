using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace backend.Models
{
    public class GolferDocV2
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public DateTime Modified { get; set; }
        public string Gender { get; set; }
        public string ObfuscatedGid { get; set; }
        [JsonProperty("scores")]
        public JArray Scores { get; set; } = new JArray();
    }
}
