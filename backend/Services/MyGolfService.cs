using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using backend.Models;
using System;
using Microsoft.ApplicationInsights;

namespace backend.Services
{

    public class MyGolfService
    {
        private CookieContainer _cookieContainer;
        private TelemetryClient telemetry;

        public MyGolfService(TelemetryClient telemetry)
        {
            this.telemetry = telemetry;
        }

        public async Task<Result> Login(Credentials creds)
        {
            try
            {
                if (!creds.UserName.Contains("-"))
                {
                    creds.UserName = creds.UserName.Insert(6, "-");
                }
                var cookieContainer = new CookieContainer();
                var request = (HttpWebRequest)WebRequest.Create("https://mingolf.golf.se/handlers/login");
                request.CookieContainer = cookieContainer;
                request.Method = HttpMethod.Post.Method;
                request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                var data = $"golfID={WebUtility.UrlEncode(creds.UserName)}&password={WebUtility.UrlEncode(creds.Password)}&remember=false";

                var reqStream = await request.GetRequestStreamAsync();
                await reqStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(data), 0, data.Length);
                var wr = (HttpWebResponse)await request.GetResponseAsync();
                if (wr.StatusCode != HttpStatusCode.OK)
                    return Result.Error($"Kunde inte ansluta till login: {wr.StatusDescription}");
                var receiveStream = wr.GetResponseStream();
                var reader = new StreamReader(receiveStream, System.Text.Encoding.UTF8);
                string content = reader.ReadToEnd();
                dynamic loginResult = JsonConvert.DeserializeObject(content);
                if (loginResult.Success != true)
                {
                    return Result.Error($"Felaktigt user/pass: golfid:{creds.UserName}");
                }
                _cookieContainer = cookieContainer;
                return Result.OK();
            }
            catch (Exception ex)
            {
                telemetry.TrackException(ex);
                return Result.Error(ex.Message);
            }
        }
        public async Task<Result<string>> GetMyGolfRawData()
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create("https://mingolf.golf.se/Site/HCP");
                request.CookieContainer = _cookieContainer;
                var wr = (HttpWebResponse)await request.GetResponseAsync();
                if (wr.StatusCode != HttpStatusCode.OK)
                    return Result.Error($"Kunde inte ansluta till hcp sida: {wr.StatusDescription}").WithData<string>(null);
                var reader = new StreamReader(wr.GetResponseStream(), System.Text.Encoding.UTF8);
                return Result.OK().WithData(reader.ReadToEnd());
            }
            catch (Exception ex)
            {
                telemetry.TrackException(ex);
                return Result.Error(ex.Message).WithData<string>(null);
            }
        }
    }
}
