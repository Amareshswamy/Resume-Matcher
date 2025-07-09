using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Threading.Tasks;

namespace JobFunctions
{
    public class DeleteJob
    {
        private readonly ILogger _logger;

        public DeleteJob(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<DeleteJob>();
        }

        [Function("DeleteJob")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "DeleteJob/{jobId}")] HttpRequestData req,
            string jobId)
        {
            _logger.LogInformation($"Deleting blob for jobId: {jobId}");

            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            string containerName = "jobs"; // Your blob container name

            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            string blobName = $"{jobId}.json";
            var blobClient = containerClient.GetBlobClient(blobName);

            var exists = await blobClient.ExistsAsync();
            if (!exists)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Job '{jobId}' not found.");
                return notFoundResponse;
            }

            await blobClient.DeleteIfExistsAsync();

            var okResponse = req.CreateResponse(HttpStatusCode.OK);
            await okResponse.WriteStringAsync($"Job '{jobId}' deleted successfully.");
            return okResponse;
        }
    }
}
