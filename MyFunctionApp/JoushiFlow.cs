using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MyFunctionApp
{
    public static class JoushiFlow
    {
        private static HttpClient httpClient = new HttpClient();

        [FunctionName("JoushiFlow")]
        public static async Task<string> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context)
        {
            // Question1
            await context.CallActivityAsync<string>("JoushiFlow_PostToSlack", $"[{context.InstanceId}] 連絡フローが開始しました。");
            var approved1 = await context.WaitForExternalEvent<bool>("JoushiFlow_Approval1");
            if (!approved1)
            {
                // denied -> exit
                return await context.CallActivityAsync<string>("JoushiFlow_PostToSlack", $"[{context.InstanceId}] Denied Question1.");
            }

            // Question2
            await context.CallActivityAsync<string>("JoushiFlow_PostToSlack", $"[{context.InstanceId}] 課長宛に連絡が来ました。'");
            var approved2 = await context.WaitForExternalEvent<bool>("JoushiFlow_Approval2");
            if (!approved2)
            {
                // denied -> exit
                return await context.CallActivityAsync<string>("JoushiFlow_PostToSlack", $"[{context.InstanceId}] Denied Question2.");
            }

            // Question3
            await context.CallActivityAsync<string>("JoushiFlow_PostToSlack", $"[{context.InstanceId}] 部長宛に連絡が来ました。");
            var approved3 = await context.WaitForExternalEvent<bool>("JoushiFlow_Approval3");
            if (!approved3)
            {
                // denied -> exit
                return await context.CallActivityAsync<string>("JoushiFlow_PostToSlack", $"[{context.InstanceId}] Denied Question3.");
            }

            return await context.CallActivityAsync<string>("JoushiFlow_PostToSlack", $"[{context.InstanceId}] 社長宛に連絡が来ました。連絡フローが完了しました。");
        }

        [FunctionName("JoushiFlow_PostToSlack")]
        public static string PostToSlack([ActivityTrigger] string message, ILogger log)
        {
            log.LogInformation(message);
            var webhookUrl = Environment.GetEnvironmentVariable("SlackWebhookUrl");
            var payload = JsonConvert.SerializeObject(new SlackPayload { Text = message });
            var requestContent = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = httpClient.PostAsync(webhookUrl, requestContent).GetAwaiter().GetResult();
            return response.ToString();
        }

        [FunctionName("JoushiFlow_Request1")]
        public static async Task RequestApproval1(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient client,
            ILogger log)
        {
            var instanceId = req.GetQueryString("instanceId");
            log.LogInformation($"JoushiFlow_Request1 with ID = '{instanceId}'.");
            await client.RaiseEventAsync(instanceId, "JoushiFlow_Approval1", true);
        }

        [FunctionName("JoushiFlow_Request2")]
        public static async Task RequestApproval2(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient client,
            ILogger log)
        {
            var instanceId = req.GetQueryString("instanceId");
            log.LogInformation($"JoushiFlow_Request2 with ID = '{instanceId}'.");
            await client.RaiseEventAsync(instanceId, "JoushiFlow_Approval2", true);
        }

        [FunctionName("JoushiFlow_Request3")]
        public static async Task RequestApproval3(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient client,
            ILogger log)
        {
            var instanceId = req.GetQueryString("instanceId");
            log.LogInformation($"JoushiFlow_Request3 with ID = '{instanceId}'.");
            await client.RaiseEventAsync(instanceId, "JoushiFlow_Approval3", true);
        }

        [FunctionName("JoushiFlow_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("JoushiFlow", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }

    public class SlackPayload
    {
        [JsonProperty("text")]
        public string Text { get; set; }
    }

    public static class HttpRequestMessageExtensions
    {
        public static string GetQueryString(this HttpRequestMessage req, string key)
        {
            if (req.Properties.ContainsKey("HttpContext"))
            {
                var httpContext = req.Properties["HttpContext"] as DefaultHttpContext;
                var query = httpContext?.Request.Query ?? null;
                if (query?.ContainsKey(key) ?? false)
                {
                    return query[key];
                }
            }
            return string.Empty;
        }
    }
}