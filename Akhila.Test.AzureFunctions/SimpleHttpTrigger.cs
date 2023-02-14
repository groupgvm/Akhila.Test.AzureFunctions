using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Akhila.Test.AzureFunctions
{
    public static class SimpleHttpTrigger
    {
        [FunctionName("NICDetails")]
        public static async Task<IActionResult> CountLetters(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            [Queue("outqueue"), StorageAccount("AzureWebJobsStorage")] ICollector<string> msg,
            [DurableClient] IDurableClient client,
            ILogger log)
        {
            log.LogInformation("NICDetails HTTP trigger function processed a request.");

            string nicNo = req.Query["nicNo"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            nicNo = nicNo ?? data?.nicNo;

            if(!string.IsNullOrWhiteSpace(nicNo))
            {
                msg.Add(nicNo);
                await client.StartNewAsync<string>("NICDetailsOrchestrator", nicNo);
            }

            return nicNo != null
                ? (ActionResult)new OkObjectResult($"Your NIC No is :  {nicNo}")
                : new BadRequestObjectResult("Please pass a nicNo on the query string or in the request body"); ;
        }

        [FunctionName("NICDetailsOrchestrator")]
        public static async Task<object> NICDetailsOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            string nicNo = context.GetInput<string>();
            try
            {
                var year = await context.CallActivityAsync<object>("GetYear", nicNo);
                var gender = await context.CallActivityAsync<object>("GetGender", nicNo);
                log.LogInformation($"Gender : {gender},\nYear : {year}");
                return $"Gender : {gender},\nYear : {year}";
            }
            catch (Exception ex)
            {
                log.LogInformation(ex.Message);
                return null;
            }
            
        }

        [FunctionName("GetGender")]
        public static string GetGender([ActivityTrigger] string nicNo, ILogger log)
        {
            log.LogInformation($"GetGender activity function triggered");
            var datesInText = nicNo.Substring(2, 3);
            var noOfDates = Int32.Parse(datesInText);
            string gender;
            if (noOfDates > 500)
            {
                gender = "Female";
            }
            else
            {
                gender = "Male";
            }
            log.LogInformation($"gender : {gender}.");
            return $"{gender}";
        }

        [FunctionName("GetYear")]
        public static string GetYear([ActivityTrigger] string nicNo, ILogger log)
        {
            log.LogInformation($"GetYear activity function triggered");
            var year = "19" + nicNo.Substring(0, 2);
            log.LogInformation($"year : {year}.");
            return $"{year}";
        }
    }
}
