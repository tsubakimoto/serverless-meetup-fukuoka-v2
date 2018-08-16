using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MyFunctionApp
{
    public static class Approval
    {
        [FunctionName("Approval_Run")]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context,
            ILogger log)
        {
            bool approved = await context.WaitForExternalEvent<bool>("Approval");
            if (approved)
            {
                // approval granted - do the approved action
                log.LogInformation($"Approval granted with ID = '{context.InstanceId}'.");
            }
            else
            {
                // approval denied - send a notification
                log.LogInformation($"Approval denied with ID = '{context.InstanceId}'.");
            }
        }

        [FunctionName("Approval_Request")]
        public static async Task RequestApproval(
            [QueueTrigger("approval-queue")] string instanceId,
            [OrchestrationClient]DurableOrchestrationClient client,
            ILogger log)
        {
            log.LogInformation($"Approval_Request with ID = '{instanceId}'.");
            await client.RaiseEventAsync(instanceId, "Approval", true);
        }

        [FunctionName("Approval_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("Approval_Run", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}