using System;
using System.Linq;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.ApplicationInsights;
using static UserInfo;

public class MyGolfDataConverter
{
    private readonly TelemetryClient telemetry;

    public MyGolfDataConverter(TelemetryClient telemetry)
    {
        this.telemetry = telemetry;
    }
    public Data ConvertToData(string[] myHCPPageData, string gitUserData, string obfustatedGid)
    {
        var data = new Data();
        try
        {
            data.User = GetUserInfo(gitUserData);
            data.User.ObfuscatedGid = obfustatedGid;
            data.Hcp = GetPlayerRounds(myHCPPageData);
        }
        catch (Exception ex)
        {
            telemetry.TrackException(ex,
            new Dictionary<string, string> {
                {nameof(myHCPPageData),string.Join("**",myHCPPageData)}
                ,{nameof(gitUserData),gitUserData}
                });
            throw;
        }
        return data;
    }
    private UserInfo GetUserInfo(string gitUserData)
    {
        var userInfo = new UserInfo();

        //for now it looks like this
        //<Gender xsi:type="xsd:unsignedByte">1</Gender>
        var match = Regex.Match(gitUserData ?? string.Empty, @"(\d)<\/Gender>");
        if (!match.Success)
        {
            telemetry.TrackException(new Exception("Unable to parse gender"));
            return userInfo;
        }
        userInfo.Gender = match.Groups[1].Value == "1" ? Sex.Male : Sex.Female;
        return userInfo;
    }
    //https://mingolf.golf.se/Site/HCP?handler=RoundItems
    private Hcp GetPlayerRounds(string[] myHCPPageDatas)
    {
        var result = new Hcp();
        result.CourseStats = new Dictionary<string, Dictionary<int, HoleStats>>();
        foreach (var myHCPPageData in myHCPPageDatas)
        {
            dynamic data;
            try
            {
                var match = Regex.Match(myHCPPageData, @"var\s*hcpRounds\s=\s({.*?});");
                if (match.Success)
                {
                    data = Newtonsoft.Json.JsonConvert.DeserializeObject<ExpandoObject>(match.Groups[1].Value);
                }
                else
                {
                    data = ((dynamic)Newtonsoft.Json.JsonConvert.DeserializeObject<ExpandoObject>(myHCPPageData)).Result;
                }
            }
            catch (Exception)
            {
                throw new Exception("Unable to parseRounds, format error, no json or hcpRounds");
            }
            if (data.Items == null) continue;

            foreach (var round in data.Items)
            {
                try
                {
                    if (!round.IsCalculated)
                    {
                        result.UncalculatedScores++;
                        continue;
                    }
                    var typedRound = new Round
                    {
                        Course = $"{round.ClubName} {round.CourseName}",
                        Date = DateTime.ParseExact(round.RoundDate, "yyyyMMddTHHmmss", CultureInfo.InvariantCulture).ToString("yyyy-MM-dd HH:mm"),
                        Hcp = round.HCPResultAdjusted.Value,
                        Holes = (int)round.PlayedHoles,
                        PCC = (int)round.PCC,
                        RoundType = (int)round.Type,
                        Score = (int)round.Points
                    };
                    result.Rounds.Add(typedRound);
                    SetPerHoleData(typedRound.Course, round.HoleScores, result.CourseStats);
                }
                catch (Exception ex)
                {
                    telemetry.TrackException(ex);
                    return result;
                }
            }
        }
        return result;
    }
    private void SetPerHoleData(string courseName, dynamic round, Dictionary<string, Dictionary<int, HoleStats>> sPerHole)
    {
        if (round == null) return;
        if (!sPerHole.ContainsKey(courseName))
        {
            sPerHole.Add(courseName, new Dictionary<int, HoleStats>());
        }
        var holeStats = sPerHole[courseName];
        foreach (var hole in round)
        {
            var holeN = (int)hole.Number;
            if (!holeStats.ContainsKey(holeN))
            {
                holeStats.Add(holeN, new HoleStats(holeN, (int)hole.Par));
            }
            holeStats[holeN].AddScore((int)hole.AdjustedGross);
        }
    }

}
public class HoleStats
{
    public int Number { get; set; }
    public int Par { get; set; }
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
    public double Average => Scores.Average();
    public int High => Scores.Max();
    public int Low => Scores.Min();
}
