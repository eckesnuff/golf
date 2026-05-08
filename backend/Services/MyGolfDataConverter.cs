using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;
using static UserInfo;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class MyGolfDataConverter
{
    private readonly TelemetryClient telemetry;

    public MyGolfDataConverter(TelemetryClient telemetry)
    {
        this.telemetry = telemetry;
    }
    public Data ConvertToData(string[] scoresJson, string personToken, string obfustatedGid, Data existingData = null)
    {
        var data = new Data();
        try
        {
            data.User = GetUserInfo(personToken);
            data.User.ObfuscatedGid = obfustatedGid;
            data.Hcp = GetPlayerRounds(scoresJson, existingData?.Hcp);
        }
        catch (Exception ex)
        {
            telemetry.TrackException(ex,
            new Dictionary<string, string> {
                {nameof(scoresJson), string.Join("**", scoresJson)}
            });
            throw;
        }
        return data;
    }
    public string GetGenderFromToken(string personToken)
    {
        try
        {
            var parts = personToken?.Split('.');
            if (parts?.Length < 2) return "";
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            dynamic claims = JsonConvert.DeserializeObject(json);
            return (string)claims.gender ?? "";
        }
        catch { return ""; }
    }

    public Data ConvertFromRawScores(JArray scores, string gender, string obfuscatedGid)
    {
        var data = new Data();
        try
        {
            data.User = new UserInfo
            {
                Gender = gender == "Male" ? UserInfo.Sex.Male : UserInfo.Sex.Female,
                ObfuscatedGid = obfuscatedGid
            };
            data.Hcp = new Hcp { CourseStats = new Dictionary<string, Dictionary<int, HoleStats>>() };
            foreach (var score in scores)
            {
                try
                {
                    if (!(bool)score["isCalculated"]) { data.Hcp.UncalculatedScores++; continue; }
                    var courseName = $"{score["clubName"]} {score["courseName"]}";
                    data.Hcp.Rounds.Add(new Round
                    {
                        Course = courseName,
                        Date = ((DateTime)score["date"]).ToString("yyyy-MM-dd HH:mm"),
                        Hcp = (double)score["hcp"],
                        Holes = (int)score["numberOfHolesPlayed"],
                        PCC = (int)score["pcc"],
                        RoundType = (string)score["type"] == "Regular" ? 0 : 1,
                        Score = (int)score["points"]
                    });
                    SetPerHoleData(courseName, score["holes"], data.Hcp.CourseStats);
                }
                catch (Exception ex) { telemetry.TrackException(ex); }
            }
        }
        catch (Exception ex) { telemetry.TrackException(ex); throw; }
        return data;
    }

    private UserInfo GetUserInfo(string personToken)
    {
        var userInfo = new UserInfo();
        try
        {
            var parts = personToken?.Split('.');
            if (parts?.Length < 2) return userInfo;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));

            dynamic claims = JsonConvert.DeserializeObject(json);
            userInfo.Gender = (string)claims.gender == "Male" ? Sex.Male : Sex.Female;
        }
        catch (Exception ex)
        {
            telemetry.TrackException(new Exception("Unable to parse personToken gender", ex));
        }
        return userInfo;
    }
    //https://mingolf.golf.se/start/api/persons/scores?offset=X
    private Hcp GetPlayerRounds(string[] scoresJsonPages, Hcp existing = null)
    {
        var result = new Hcp();
        result.CourseStats = existing?.CourseStats ?? new Dictionary<string, Dictionary<int, HoleStats>>();

        var latestDateStr = existing?.Rounds?.Count > 0
            ? existing.Rounds.Max(r => r.Date)
            : null;

        foreach (var pageJson in scoresJsonPages)
        {
            dynamic page = JsonConvert.DeserializeObject(pageJson);
            if (page.scores == null) continue;

            foreach (var score in page.scores)
            {
                try
                {
                    if (!(bool)score.isCalculated)
                    {
                        result.UncalculatedScores++;
                        continue;
                    }

                    var roundDateStr = ((DateTime)score.date).ToString("yyyy-MM-dd HH:mm");
                    if (latestDateStr != null && string.Compare(roundDateStr, latestDateStr) <= 0)
                        continue;

                    var courseName = $"{score.clubName} {score.courseName}";
                    var typedRound = new Round
                    {
                        Course = courseName,
                        Date = roundDateStr,
                        Hcp = (double)score.hcp,
                        Holes = (int)score.numberOfHolesPlayed,
                        PCC = (int)score.pcc,
                        RoundType = (string)score.type == "Regular" ? 0 : 1,
                        Score = (int)score.points
                    };
                    result.Rounds.Add(typedRound);
                    SetPerHoleData(courseName, score.holes, result.CourseStats);
                }
                catch (Exception ex)
                {
                    telemetry.TrackException(ex);
                    return result;
                }
            }
        }

        if (existing != null)
        {
            result.Rounds.AddRange(existing.Rounds);
            result.UncalculatedScores += existing.UncalculatedScores;
        }

        return result;
    }
    private void SetPerHoleData(string courseName, dynamic holes, Dictionary<string, Dictionary<int, HoleStats>> sPerHole)
    {
        if (holes == null) return;
        if (!sPerHole.ContainsKey(courseName))
        {
            sPerHole.Add(courseName, new Dictionary<int, HoleStats>());
        }
        var holeStats = sPerHole[courseName];
        foreach (var hole in holes)
        {
            var holeN = (int)hole.number;
            if (!holeStats.ContainsKey(holeN))
            {
                holeStats.Add(holeN, new HoleStats(holeN, (int)hole.par));
            }
            holeStats[holeN].AddScore((int)hole.brutto);
        }
    }

}
public class HoleStats
{
    [JsonProperty("number")]
    public int Number { get; set; }
    [JsonProperty("par")]
    public int Par { get; set; }
    [JsonProperty("scores")]    
    public List<int> Scores { get; set; } = new List<int>();
    public HoleStats(int number, int par)
    {
        Number = number;
        Par = par;
    }
    public void AddScore(int grossAdjusted)
    {
        Scores.Add(grossAdjusted);
    }
    [JsonProperty("average")]
    public double Average => Scores.Average();
    [JsonProperty("high")]    
    public int High => Scores.Max();
    [JsonProperty("low")]
    public int Low => Scores.Min();
}
