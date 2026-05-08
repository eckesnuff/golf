using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Newtonsoft.Json;
using backend.Models;

namespace backend.Services
{
    public class MyGolfService
    {
        private HttpClient _sessionClient;
        private string _accessToken;
        private readonly TelemetryClient telemetry;

        public MyGolfService(TelemetryClient telemetry)
        {
            this.telemetry = telemetry;
        }

        public async Task<Result<string>> Login(Credentials creds)
        {
            try
            {
                if (!creds.UserName.Contains("-"))
                    creds.UserName = creds.UserName.Insert(6, "-");

                _sessionClient?.Dispose();
                _sessionClient = new HttpClient();

                var content = new StringContent(
                    JsonConvert.SerializeObject(new { GolfId = creds.UserName, Password = creds.Password }),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var response = await _sessionClient.PostAsync("https://mingolf.golf.se/login/api/Users/Login", content);
                if (!response.IsSuccessStatusCode)
                    return Result.Error($"Felaktigt user/pass: golfid:{creds.UserName}").WithData<string>(null);

                dynamic loginResult = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
                _accessToken = (string)loginResult.accessToken;
                var personToken = (string)loginResult.personToken;

                return Result.OK().WithData(personToken);
            }
            catch (Exception ex)
            {
                telemetry.TrackException(ex);
                return Result.Error(ex.Message).WithData<string>(null);
            }
        }

        public async Task<Result<string[]>> GetMyGolfRawData(string latestKnownDate = null)
        {
            try
            {
                var results = new List<string>();
                var offset = 0;
                while (true)
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, $"https://mingolf.golf.se/start/api/persons/scores?offset={offset}");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                    var response = await _sessionClient.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                        return Result.Error($"Kunde inte hämta rundor: {response.ReasonPhrase}").WithData<string[]>(null);

                    var json = await response.Content.ReadAsStringAsync();
                    results.Add(json);

                    dynamic page = JsonConvert.DeserializeObject(json);
                    int totalCount = (int)page.totalCount;
                    if (totalCount < 50) break;

                    if (latestKnownDate != null)
                    {
                        var lastScore = page.scores[totalCount - 1];
                        var lastDateStr = ((DateTime)lastScore.date).ToString("yyyy-MM-dd HH:mm");
                        if (string.Compare(lastDateStr, latestKnownDate) <= 0) break;
                    }

                    offset += 50;
                }
                return Result.OK().WithData(results.ToArray());
            }
            catch (Exception ex)
            {
                telemetry.TrackException(ex);
                return Result.Error(ex.Message).WithData<string[]>(null);
            }
        }
    }
}
