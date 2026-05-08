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
            sb.AppendLine("  hcp: exact handicap index after the round");
            sb.AppendLine("  points: Stableford score for the full round");
            sb.AppendLine("  numberOfHolesPlayed: 9 or 18");
            sb.AppendLine("  pcc: Playing Conditions Calculation adjustment");
            sb.AppendLine("  type: Regular or competition");
            sb.AppendLine("  isCalculated: whether the round counts toward handicap");
            sb.AppendLine("  holes[].number: hole number");
            sb.AppendLine("  holes[].par: par for the hole (3, 4 or 5)");
            sb.AppendLine("  holes[].brutto: gross score (actual strokes taken on that hole)");
            sb.AppendLine();
            sb.AppendLine("Golf scoring vs par (based on brutto vs par):");
            sb.AppendLine("  brutto = par - 2 → eagle");
            sb.AppendLine("  brutto = par - 1 → birdie");
            sb.AppendLine("  brutto = par     → par");
            sb.AppendLine("  brutto = par + 1 → bogey");
            sb.AppendLine("  brutto = par + 2 → double bogey");
            sb.AppendLine("  Example: brutto 2 on a par 3 = birdie (NOT eagle)");
            sb.AppendLine();
            sb.AppendLine("Rounds (newest first):");
            sb.AppendLine(scores.ToString(Newtonsoft.Json.Formatting.None));
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
    
