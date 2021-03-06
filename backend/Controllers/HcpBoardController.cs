﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Mvc;
using backend.Services;
using backend.Models;
using Microsoft.AspNetCore.Hosting;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
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

        public HcpBoardController(TelemetryClient telemetry, IWebHostEnvironment env, Persistence persistence, IConfiguration configuration,MyGolfService myGolfService)
        {
            this.myGolfService = myGolfService;
            this.telemetry = telemetry;
            this.env = env;
            this.persistence = persistence;
            this.configuration = configuration;
            dataConverter = new MyGolfDataConverter(telemetry);
        }
        // GET api/hcpboard
        [HttpGet]
        public async Task<ActionResult<Result>> Get(string userHash)
        {
            var result= await persistence.GetGolferAsync(userHash);
            if(result==null){
                return NotFound();
            }
            return Result.OK().WithData(result.Data);
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

            var result = await myGolfService.Login(creds);
            if (!result.Success)
            {
                return Unauthorized(result);
            }
            var hash=GenerateUniqueId(creds.UserName);
            this.HttpContext.User= new GenericPrincipal(new GenericIdentity(hash),new string[]{"golfer"});

            var persistantTask = persistence.GetGolferAsync(hash);
            var myGolfDataTask = myGolfService.GetMyGolfRawData();
            var gitTask = myGolfService.GetGolfMatrikel(creds.UserName);
            await Task.WhenAll(persistantTask, myGolfDataTask,gitTask);
            GolferDoc doc = null;
            if (persistantTask.IsCompletedSuccessfully)
            {
                doc = persistantTask.Result;
            }
            if (myGolfDataTask.Result.Success)
            {
                var convertedResult = dataConverter.ConvertToData(myGolfDataTask.Result.Data,gitTask.Result.Data ,obfuscatedGid);
                if (convertedResult.IsValid())
                {
                    await persistence.SaveGolferAsync(new GolferDoc
                    {
                        Modified = DateTime.UtcNow,
                        Data = convertedResult,
                        Id = hash
                    });
                    return Result.OK().WithData(convertedResult);
                }
                else
                {
                    return Result.Error("Kunde inte parsa rundor");
                }
            }
            else if (doc != null)
            {
                return Result.OK($"Kunde inte hämta från min golf, Rundorna är från {doc.Modified.ToString("d")}").WithData(doc.Data);
            }
            return myGolfDataTask.Result;

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
    
