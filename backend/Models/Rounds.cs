using System.Collections.Generic;
using Newtonsoft.Json;

public class Data
{
    [JsonProperty("hcp")]
    public Hcp Hcp { get; set; }
    [JsonProperty("user")]
    public UserInfo User { get; set; }
    public bool IsValid()
    {
        return Hcp?.Rounds?.Count > 0 && User?.Gender > 0;
    }
}

public class UserInfo
{
    [JsonProperty("gender")]
    public Sex Gender { get; set; }
    [JsonProperty("oGid")]
    public string ObfuscatedGid{get;set;}
    public enum Sex
    {
        Androgynous = 0,
        Male = 1,
        Female = 2
    }
}
public class Hcp
{
    [JsonProperty("rounds")]
    public List<Round> Rounds = new List<Round>();
    [JsonProperty("nUncalculatedScores")]
    public int UncalculatedScores { get; set; }
}

public class Round
{
    [JsonProperty("date")]
    public string Date { get; set; }
    [JsonProperty("course")]
    public string Course { get; set; }
    [JsonProperty("roundType")]
    public int RoundType { get; set; }
    [JsonProperty("nHoles")]
    public int Holes { get; set; }
    [JsonProperty("score")]
    public int Score { get; set; }
    [JsonProperty("hcp")]
    public double Hcp { get; set; }
    [JsonProperty("pcc")]
    public int PCC { get; set; }
}