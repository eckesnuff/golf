﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HcpBoardController : ControllerBase
    {
        private readonly WebRequester webRequester;
        public HcpBoardController(TelemetryClient telemetry)
        {
            webRequester = new WebRequester(telemetry);
        }
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
            if (!ModelState.IsValid)
            {
                return WorkResult.Error("user/pass format");
            }
            if (!credentials.UserName.Contains("-"))
            {
                credentials.UserName = credentials.UserName.Insert(6, "-");
            }
            try
            {
                return await webRequester.DoWork(credentials);
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
        [RegularExpression(@"^\d{6}-?\d{3}$",ErrorMessage="Fel format på golfId")]
        public string UserName { get; set; }
        [Required(ErrorMessage="Password missing")]
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
        private TelemetryClient telemetry;

        public WebRequester(TelemetryClient telemetry)
        {
            this.telemetry = telemetry;
        }

        public async Task<WorkResult> DoWork(Credentials creds)
        {
            telemetry.TrackEvent("user",new Dictionary<string,string>{
                {"id",creds.UserName.Substring(creds.UserName.Length - 3)}
            });
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
                return WorkResult.Error($"Felaktigt user/pass: golfid:{creds.UserName}");
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
