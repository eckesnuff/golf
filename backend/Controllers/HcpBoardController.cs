using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using backend.Services;
using backend.Models;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Security.Claims;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HcpBoardController : ControllerBase
    {
        private readonly MyGolfService myGolfService;
        private readonly TelemetryClient telemetry;
        private readonly IWebHostEnvironment env;
        private readonly Persistence persistence;
        private readonly IConfiguration configuration;
        private readonly MyGolfDataConverter dataConverter;
        private readonly IMemoryCache _cache;

        public HcpBoardController(TelemetryClient telemetry, IWebHostEnvironment env, Persistence persistence, IConfiguration configuration, MyGolfService myGolfService, IMemoryCache cache)
        {
            this.myGolfService = myGolfService;
            this.telemetry = telemetry;
            this.env = env;
            this.persistence = persistence;
            this.configuration = configuration;
            dataConverter = new MyGolfDataConverter(telemetry);
            _cache = cache;
        }
        // GET api/hcpboard
        [HttpGet]
        public async Task<ActionResult<Result>> Get(string userHash)
        {
            var result = await persistence.GetGolferV2Async(userHash);
            if (result == null)
                return NotFound();
            var data = dataConverter.ConvertFromRawScores(result.Scores, result.Gender, result.ObfuscatedGid);
            return Result.OK().WithData(data);
        }

        // POST api/hcpboard
        [HttpPost]
        public async Task<ActionResult<Result>> Post(Credentials creds)
        {
            if (!ModelState.IsValid)
            {
                return Unauthorized(Result.Error("Felaktigt golf-id eller lösenord"));
            }
            var obfuscatedGid=creds.UserName.Substring(0,6);
            telemetry.TrackEvent("user", new Dictionary<string, string>{
                {"id",obfuscatedGid}});

            var loginResult = await myGolfService.Login(creds);
            if (!loginResult.Success)
            {
                return Unauthorized(loginResult);
            }
            var hash=GenerateUniqueId(creds.UserName);
            this.HttpContext.User= new GenericPrincipal(new GenericIdentity(hash),new string[]{"golfer"});

            var existingDoc = await persistence.GetGolferV2Async(hash);
            var latestKnownDate = existingDoc?.Scores?.Count > 0
                ? existingDoc.Scores.Max(s => ((DateTime)s["date"]).ToString("yyyy-MM-dd HH:mm"))
                : null;
            var myGolfData = await myGolfService.GetMyGolfRawData(latestKnownDate);

            if (myGolfData.Success)
            {
                var fetchedScores = new JArray();
                var fetchedIds = new HashSet<string>();
                foreach (var pageJson in myGolfData.Data)
                {
                    dynamic page = JsonConvert.DeserializeObject(pageJson);
                    if (page.scores == null) continue;
                    foreach (var score in page.scores)
                    {
                        fetchedIds.Add((string)score.id);
                        fetchedScores.Add(score);
                    }
                }

                // fetched scores win; keep existing only for ids not covered by the fetch
                var mergedScores = new JArray();
                foreach (var s in fetchedScores) mergedScores.Add(s);
                if (existingDoc?.Scores != null)
                    foreach (var s in existingDoc.Scores)
                    {
                        if (!fetchedIds.Contains((string)s["id"]))
                            mergedScores.Add(s);
                    }

                if (mergedScores.Count == 0)
                    return Result.Error("Kunde inte parsa rundor");

                var gender = dataConverter.GetGenderFromToken(loginResult.Data);
                await persistence.SaveGolferV2Async(new GolferDocV2
                {
                    Id = hash,
                    Modified = DateTime.UtcNow,
                    Gender = gender,
                    ObfuscatedGid = obfuscatedGid,
                    Scores = mergedScores
                });
                var convertedResult = dataConverter.ConvertFromRawScores(mergedScores, gender, obfuscatedGid);
                if (!convertedResult.IsValid())
                    return Result.Error("Kunde inte parsa rundor");

                var sessionToken = Guid.NewGuid().ToString("N");
                _cache.Set(sessionToken, new GolferSession
                {
                    SystemContext = BuildSystemContext(mergedScores, gender, obfuscatedGid)
                }, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(10) });

                var result = Result.OK().WithData(convertedResult);
                result.SessionToken = sessionToken;
                return result;
            }
            else if (existingDoc != null)
            {
                var fallback = dataConverter.ConvertFromRawScores(existingDoc.Scores, existingDoc.Gender, existingDoc.ObfuscatedGid);
                return Result.OK($"Kunde inte hämta från min golf, Rundorna är från {existingDoc.Modified.ToString("d")}").WithData(fallback);
            }
            return myGolfData;

        }

        private static JArray TrimScoresForChat(JArray scores)
        {
            var result = new JArray();
            foreach (JObject score in scores)
            {
                var trimmed = new JObject
                {
                    ["date"]              = score["date"],
                    ["clubName"]          = score["clubName"],
                    ["courseName"]        = score["courseName"],
                    ["playingHcp"]        = score["playingHcp"],
                    ["adjustedHcp"]       = score["adjustedHcp"],
                    ["par"]               = score["par"],
                    ["tee"]               = score["tee"],
                    ["numberOfHolesPlayed"] = score["numberOfHolesPlayed"],
                    ["markerName"]        = score["markerName"]
                };

                if (score["holes"] is JArray holes)
                {
                    var trimmedHoles = new JArray();
                    foreach (JObject hole in holes)
                        trimmedHoles.Add(new JObject
                        {
                            ["number"] = hole["number"],
                            ["par"]    = hole["par"],
                            ["brutto"] = hole["brutto"]
                        });
                    trimmed["holes"] = trimmedHoles;
                }

                result.Add(trimmed);
            }
            return result;
        }

        private string BuildSystemContext(JArray scores, string gender, string obfuscatedGid)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are a golf performance analyst. Answer questions about the player's golf rounds.");
            sb.AppendLine();
            sb.AppendLine($"Player: {obfuscatedGid} | Gender: {gender}");
            sb.AppendLine();
            sb.AppendLine("Field reference:");
            sb.AppendLine("  date: when the round was played");
            sb.AppendLine("  clubName / courseName: venue");
            sb.AppendLine("  playingHcp: the handicap strokes the player was allocated for the round");
            sb.AppendLine("  adjustedHcp: adjusted handicap after the round — can be higher than playingHcp if the player scored worse than their allocation (e.g. playingHcp 5 but played like a 7)");
            sb.AppendLine("  par: course par");
            sb.AppendLine("  tee: tee played from");
            sb.AppendLine("  numberOfHolesPlayed: 9 or 18");
            sb.AppendLine("  markerName: name of the marker/scorer");
            sb.AppendLine("  holes[].number: hole number");
            sb.AppendLine("  holes[].par: par for the hole (3, 4 or 5)");
            sb.AppendLine("  holes[].brutto: gross score (actual strokes taken on that hole)");
            sb.AppendLine();
            sb.AppendLine("Golf scoring terms — always derived from hole.brutto (actual shots taken) vs hole.par (shots needed for par):");
            sb.AppendLine("  HIO / Hole in one: brutto = 1, regardless of par");
            sb.AppendLine("  Albatross:         brutto = par - 3");
            sb.AppendLine("  Eagle:             brutto = par - 2");
            sb.AppendLine("  Birdie:            brutto = par - 1");
            sb.AppendLine("  Par:               brutto = par");
            sb.AppendLine("  Bogey:             brutto = par + 1");
            sb.AppendLine("  Double bogey:      brutto = par + 2");
            sb.AppendLine("  Example: brutto 2 on a par 3 = birdie (one less than par, NOT eagle)");
            sb.AppendLine();
            sb.AppendLine("Rounds (newest first):");
            sb.AppendLine(TrimScoresForChat(scores).ToString(Newtonsoft.Json.Formatting.None));
            return sb.ToString();
        }

        private string GenerateUniqueId(string golfId)
        {
            var salt = configuration["Salt"];
            var input = golfId+salt;
            using (MD5 md5Hash = MD5.Create())
            {
                byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder sBuilder = new StringBuilder();
                for (int i = 0; i < data.Length; i++)
                {
                    sBuilder.Append(data[i].ToString("x2"));
                }
                return sBuilder.ToString();
            }
        }
    }
}
    
