using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HcpBoardController : ControllerBase
    {
        // GET api/hcpboard
        [HttpGet]
        public ActionResult<IEnumerable<string>> Get()
        {
            return new string[] { "Works", "OK" };
        }

        // POST api/hcpboard
        [HttpPost]
        public async Task<ActionResult<WorkResult>> Post(Credentials credentials)
        {
            try
            {
                var service = new WebRequester();
                return await service.DoWork(credentials);
            }
            catch (Exception ex)
            {
                return new WorkResult
                {
                    Success = false,
                    ResultData = ex.ToString()
                };
            }
        }
    }
    public class Credentials
    {
        public string UserName { get; set; }
        public string Password { get; set; }
    }
    public class WorkResult
    {
        public static WorkResult Error(string message)
        {
            return new WorkResult
            {
                Success = false,
                ResultData = message
            };
        }
        public static WorkResult OK(string message)
        {
            return new WorkResult
            {
                Success = true,
                ResultData = message
            };
        }
        public bool Success { get; set; }
        public string ResultData { get; set; }
    }
    public class WebRequester
    {
        public async Task<WorkResult> DoWork(Credentials creds)
        {
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
                return WorkResult.Error($"Kunde inte ansluta till login: {wr.StatusDescription}");
            var receiveStream = wr.GetResponseStream();
            var reader = new StreamReader(receiveStream, System.Text.Encoding.UTF8);
            string content = reader.ReadToEnd();
            dynamic loginResult = JsonConvert.DeserializeObject(content);
            if (loginResult.Success != true)
            {
                return WorkResult.Error($"Kunde inte ansluta: {wr.StatusDescription}");
            }

            request = (HttpWebRequest)WebRequest.Create("https://mingolf.golf.se/Site/HCP");
            request.CookieContainer = cookieContainer;
            wr = (HttpWebResponse)await request.GetResponseAsync();
            if (wr.StatusCode != HttpStatusCode.OK)
                return WorkResult.Error($"Kunde inte ansluta till hcp sida: {wr.StatusDescription}");
            receiveStream = wr.GetResponseStream();
            reader = new StreamReader(receiveStream, System.Text.Encoding.UTF8);
            content = reader.ReadToEnd();
            var match = Regex.Match(content, @"var\s*hcpRounds\s=\s({.*?});");
            return match.Success ? WorkResult.OK(match.Groups[1].Value) : WorkResult.Error($"Unable to parse text: {content}");
        }
    }
}
