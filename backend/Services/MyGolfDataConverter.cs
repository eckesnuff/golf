using System;
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
    public Data ConvertToData(string unparsedData,string obfustatedGid)
    {
        var user = GetUserInfo(unparsedData);
        user.ObfuscatedGid=obfustatedGid;
        return new Data{
            Hcp=GetPlayerRounds(unparsedData),
            User=user
        };
    }
    private UserInfo GetUserInfo(string unparsedData)
    {
        var userInfo = new UserInfo();
        //for now it looks like this
        //googletag.pubads().setTargeting('gen', ['man']);
        var match = Regex.Match(unparsedData, @"'gen'\s*,\s*\[\s*'(.*?)'\s*\]");
        if (!match.Success){
            telemetry.TrackException(new Exception("Unable to parse gender"));
            return userInfo;
        }
        userInfo.Gender=match.Groups[1].Value=="man"?Sex.Male:Sex.Female;
        return userInfo;
    }
    private Hcp GetPlayerRounds(string unparsedData)
    {
        var match = Regex.Match(unparsedData, @"var\s*hcpRounds\s=\s({.*?});");
        if (!match.Success)
        {
            telemetry.TrackException(new Exception("Unable to parse rounds"));
            return null;
        }
        dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject<ExpandoObject>(match.Groups[1].Value);
        var result = new Hcp();
        foreach (var round in data.Items)
        {
            if (!round.IsCalculated)
            {
                result.UncalculatedScores++;
                continue;
            }
            result.Rounds.Add(new Round
            {
                Course = $"{round.ClubName} {round.CourseName}",
                Date = DateTime.ParseExact(round.RoundDate, "yyyyMMddTHHmmss", CultureInfo.InvariantCulture).ToString("yyyy-MM-dd HH:mm"),
                Hcp = round.HCPResultAdjusted.Value,
                Holes = (int)round.PlayedHoles,
                PCC = (int)round.PCC,
                RoundType = (int)round.Type,
                Score = (int)round.Points
            });

        }
        return result;
    }
}