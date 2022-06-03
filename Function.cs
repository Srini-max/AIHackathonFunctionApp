using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using RestSharp;
using System.Net;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Collections.Generic;

namespace AIHackathonFunctionApp
{
    public static class Function
    {
        private static int statusCode;

        [FunctionName("AzureSearchFunction")]
        public static async Task<AzureSearchResponse> AzureSearchFunction(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, ILogger log)
        {
            var message = new JsonSerializer().Deserialize<AnalyzeTextURLReq>(new JsonTextReader(new StreamReader(req.Body)));
            string responseMessage = AnalyzeTextURLAsync(message);

            var Searchresponse = JsonConvert.DeserializeObject<AzureSearchResponse>(AzureSearch(responseMessage));
            return await Task.FromResult(Searchresponse);
        }
        public class AnalyzeTextURLReq
        {
            public string url { get; set; }
        }

        public static string AnalyzeTextURLAsync(AnalyzeTextURLReq ATUR)
        {
            var config = new ConfigurationBuilder()
                           .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                           .AddEnvironmentVariables()
                           .Build();
            string ANALYZE_TEXT_URL = config["ANALYZE_TEXT_URL"];
            var client = new RestClient(ANALYZE_TEXT_URL);
            Console.WriteLine(ANALYZE_TEXT_URL);
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "text/json");
            string profanitysubscriptionkey = config["OCP_APIM_SUBSCRIPTION_KEY"];
            request.AddHeader("Ocp-Apim-Subscription-Key", profanitysubscriptionkey);
            var requestModel = JsonConvert.SerializeObject(ATUR, new JsonSerializerSettings());
            request.AddJsonBody(requestModel);
            client.FollowRedirects = false;
            IRestResponse response = client.Execute(request);
            Console.WriteLine(response.Headers);
            string location = "";
            int code = (int)response.StatusCode;
            var textrender = "";
            if (code == 202 || code == 201 || code ==200)
            {
                if (response.Headers.Any(t => t.Name == "Operation-Location"))
                {
                    location = response.Headers.FirstOrDefault(t => t.Name == "Operation-Location").Value.ToString();
                }
                Console.WriteLine(location);
                System.Threading.Thread.Sleep(1000);
                textrender = ReadTextFromAnalyzeURL(location);
            }
            else
            {
                Console.WriteLine("An unknown exception occured");
                textrender = "An unknown exception occured";
            }
            return textrender;


        }
        public static bool IsSuccessStatusCode
        {
            get { return ((int)statusCode >= 200) && ((int)statusCode <= 299); }
        }
        public static string ReadTextFromAnalyzeURL(string url)
        {
            var config = new ConfigurationBuilder()
                           .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                           .AddEnvironmentVariables()
                           .Build();
            var client = new RestClient(url);
            Console.WriteLine(url);
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Content-Type", "text/json");
            string profanitysubscriptionkey = config["OCP_APIM_SUBSCRIPTION_KEY"];
            request.AddHeader("Ocp-Apim-Subscription-Key", profanitysubscriptionkey);
            client.FollowRedirects = false;
            IRestResponse response = client.Execute(request);
            string tmp = "";
            if (response.StatusCode == HttpStatusCode.OK)
            {
                JObject txtjson = JObject.Parse(response.Content);
                Console.WriteLine(txtjson.ToString());
                foreach (JToken token in txtjson.SelectTokens("analyzeResult.readResults[*].lines[*].text"))
                {
                    Console.WriteLine(token.Path + ": " + token);
                    tmp = tmp + token;
                }
                Console.WriteLine(tmp);
            }
            tmp = Regex.Replace(tmp, @"[^ 0-9a-zA-Z]+", "");
            return tmp;
        }

        public static string AzureSearch(string text)
        {
            var config = new ConfigurationBuilder()
                           .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                           .AddEnvironmentVariables()
                           .Build();
            string AZURE_SEARCH_URL = config["AZURE_SEARCH_URL"];
            var client = new RestClient(AZURE_SEARCH_URL + text);
            Console.WriteLine(AZURE_SEARCH_URL + text);
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Content-Type", "application/json");
            string AZURE_ADMIN_KEY = config["AZURE_ADMIN_KEY"];
            request.AddHeader("api-key", AZURE_ADMIN_KEY);
            client.FollowRedirects = false;
            IRestResponse response = client.Execute(request);
            string tmp = response.Content;
            return tmp;
        }
        
        public class AzureSearchResponse
        {

            [JsonProperty("@odata.context")]
            public string OdataContext { get; set; }
            public List<Value> value { get; set; }
        }

        public class Value
        {
            [JsonProperty("@search.score")]
            public double SearchScore { get; set; }
            public string id { get; set; }
            public string Title { get; set; }
            public string Content { get; set; }
            public string ImageURL { get; set; }
        }
    }
}