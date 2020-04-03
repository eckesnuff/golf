using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PuppeteerSharp;

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
                var service = new WebBrowser();
                return await service.DoWork(credentials.UserName, credentials.Password);
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
    public class WebBrowser
    {
        public async Task<WorkResult> DoWork(string username, string password)
        {
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
            var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true
            });
            var page = await browser.NewPageAsync();
            await page.SetRequestInterceptionAsync(true);
            var post = true;
            page.Request += async (sender, e) =>
            {
                if (!post) return;
                var payload = new Payload()
                {
                    Headers = new Dictionary<string, string>{
                            {"Content-Type", "application/x-www-form-urlencoded; charset=UTF-8"}
                        },
                    Method = HttpMethod.Post,
                    PostData = $"golfID={WebUtility.UrlEncode(username)}&password={WebUtility.UrlEncode(password)}&remember=false",
                };
                await e.Request.ContinueAsync(payload);
                await page.SetRequestInterceptionAsync(false);
                post = false;
            };
            var response = await page.GoToAsync("https://mingolf.golf.se/handlers/login");
            dynamic loginResult = await response.JsonAsync();
            if (loginResult.Success == true)
            {
                var hcpReponse = await page.GoToAsync("https://mingolf.golf.se/Site/HCP");
                if (hcpReponse.Status == HttpStatusCode.OK)
                {
                    var text = await hcpReponse.TextAsync();
                    var match = Regex.Match(text, @"var\s*hcpRounds\s=\s({.*?});");
                    return match.Success ? WorkResult.OK(match.Groups[1].Value) : WorkResult.Error($"Unable to parse text: {text}");
                }
                return WorkResult.Error($"Status from hcp page was: {hcpReponse.Status} text: {hcpReponse.StatusText}");
            }
            return WorkResult.Error("Kunde inte logga in");
        }

    }
}
