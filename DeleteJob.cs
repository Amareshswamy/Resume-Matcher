using Azure;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace JobFunctions
{
    public static class DeleteJobFromBlob
    {
        [FunctionName("DeleteJob")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "DeleteJob/{jobId}")] HttpRequest req,
            string jobId,
            ILogger log)
        {
            log.LogInformation($"Deleting blob for jobId: {jobId}");

            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            string containerName = "jobs"; // your blob container name

            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            // Assuming blob name is "{jobId}.json"
            string blobName = $"{jobId}.json";
            var blobClient = containerClient.GetBlobClient(blobName);

            // Check if the blob exists
            var exists = await blobClient.ExistsAsync();
            if (!exists)
            {
                return new NotFoundObjectResult($"Job '{jobId}' not found.");
            }

            // Delete the blob
            await blobClient.DeleteIfExistsAsync();

            return new OkObjectResult($"Job '{jobId}' deleted successfully.");
        }
    }
}
