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
    public Data ConvertToData(string myHCPPageData, string gitUserData,string obfustatedGid)
    {
        var user = GetUserInfo(gitUserData);
        user.ObfuscatedGid=obfustatedGid;
        return new Data{
            Hcp=GetPlayerRounds(myHCPPageData),
            User=user
        };
    }
    private UserInfo GetUserInfo(string gitUserData)
    {
        var userInfo = new UserInfo();

        //for now it looks like this
        //<Gender xsi:type="xsd:unsignedByte">1</Gender>
        var match = Regex.Match(gitUserData, @"(\d)<\/Gender>");
        if (!match.Success){
            telemetry.TrackException(new Exception("Unable to parse gender"));
            return userInfo;
        }
        userInfo.Gender=match.Groups[1].Value=="1"?Sex.Male:Sex.Female;
        return userInfo;
    }
    private Hcp GetPlayerRounds(string myHCPPageData)
    {
        var match = Regex.Match(myHCPPageData, @"var\s*hcpRounds\s=\s({.*?});");
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