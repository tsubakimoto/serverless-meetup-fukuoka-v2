using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace MyFunctionApp
{
    public static class FanOutFanIn
    {
        [FunctionName("FanOutFanIn_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("FanOutFanIn", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("FanOutFanIn")]
        public static async Task Run(
            [OrchestrationTrigger] DurableOrchestrationContext context)
        {
            var parallelTasks = new List<Task<int>>();

            // get a list of N work items to process in parallel
            int[] numbers = await context.CallActivityAsync<int[]>("FanOutFanIn_Numbers", 1);
            for (int i = 0; i < numbers.Length; i++)
            {
                Task<int> task = context.CallActivityAsync<int>("FanOutFanIn_Product", numbers[i]);
                parallelTasks.Add(task);
            }

            await Task.WhenAll(parallelTasks);

            // aggregate all N outputs and send result to F3
            int sum = parallelTasks.Sum(t => t.Result);
            await context.CallActivityAsync("FanOutFanIn_SayNumber", sum);
        }

        [FunctionName("FanOutFanIn_Numbers")]
        public static int[] GetNumbers(
            [ActivityTrigger] int baseNumber,
            ILogger log)
        {
            log.LogInformation($"Base number is {baseNumber}");
            return new int[] { baseNumber, baseNumber + 1, baseNumber + 2 };
        }

        [FunctionName("FanOutFanIn_Product")]
        public static int GetProduct(
            [ActivityTrigger] int number,
            ILogger log)
        {
            log.LogInformation($"Number is {number}");
            return number * 2;
        }

        [FunctionName("FanOutFanIn_SayNumber")]
        public static void SayNumber(
            [ActivityTrigger] int sum,
            ILogger log)
        {
            log.LogInformation($"Sum is {sum}");
        }
    }
}
