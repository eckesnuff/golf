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
        private readonly GitCredentials gitCredentials;

        public MyGolfService(TelemetryClient telemetry,GitCredentials gitCredentials)
        {
            this.telemetry = telemetry;
            this.gitCredentials = gitCredentials;
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
        public async Task<Result<string>> GetGolfMatrikel(string golfId){
            try{
                var request = (HttpWebRequest)WebRequest.Create("http://gitsys.golf.se/WSAPI/Ver_3/Member/Member3.asmx");
                request.Method = HttpMethod.Post.Method;
                request.ContentType = "text/xml; charset=utf-8";
                request.Headers.Add("SOAPAction", "\"http://gitapi.golf.se/Member/Member3/GetMemberMatrikelData\"");

                var reqStream = await request.GetRequestStreamAsync();
                var data = GetEnvelope(golfId);
                await reqStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(data), 0, data.Length);
                var wr = (HttpWebResponse)await request.GetResponseAsync();
                if (wr.StatusCode != HttpStatusCode.OK)
                    return Result.Error($"Kunde inte ansluta till git: {wr.StatusDescription}").WithData<string>(null);
                var receiveStream = wr.GetResponseStream();
                var reader = new StreamReader(receiveStream, System.Text.Encoding.UTF8);
                string content = reader.ReadToEnd();
                return Result.OK().WithData<string>(content);
            }
            catch(Exception ex){
                telemetry.TrackException(ex);
                return Result.Error(ex.Message).WithData<string>(null);
            }
        }
        private string GetEnvelope(string userId){
            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soapenc=""http://schemas.xmlsoap.org/soap/encoding/"" xmlns:tns=""http://gitapi.golf.se/Member/Member3"" xmlns:types=""http://gitapi.golf.se/Member/Member3/encodedTypes"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Header>
    <tns:SoapAuthenticationHeader>
      <user xsi:type=""xsd:string"">{gitCredentials.UserName}</user>
      <password xsi:type=""xsd:string"">{gitCredentials.Password}</password>
    </tns:SoapAuthenticationHeader>
  </soap:Header>
  <soap:Body soap:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
    <tns:GetMemberMatrikelData>
      <organizationalUnitID xsi:type=""xsd:string"">{gitCredentials.OrgId}</organizationalUnitID>
      <golfID xsi:type=""xsd:string"">{userId}</golfID>
      <memberType xsi:type=""tns:MemberTypes"">ALL</memberType>
    </tns:GetMemberMatrikelData>
  </soap:Body>
</soap:Envelope>";
        }
    }

    public class GitCredentials
    {
        public string UserName{get;set;}
        public string Password{get;set;}
        public string OrgId{get;set;}
    }
}
