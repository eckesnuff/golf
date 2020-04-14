
using System;
using Newtonsoft.Json;

namespace backend.Models
{   public class GolferDoc
    {
        [JsonProperty(PropertyName = "id")]        
        public string Id { get; set;}
        public DateTime Modified { get; set; }
        public Data Data { get; set; }
    }
}