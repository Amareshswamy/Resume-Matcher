using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace jobresumefuncProj
{
    public class ListJobs
    {
        [Function("ListJobs")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            string containerName = "jobs";

            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            var jobs = new List<Job>();

            await foreach (var blobItem in containerClient.GetBlobsAsync())
            {
                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                var download = await blobClient.DownloadContentAsync();
                var json = download.Value.Content.ToString();
                var job = JsonSerializer.Deserialize<Job>(json);

                if (job != null)
                {
                    jobs.Add(job);
                }
            }

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteAsJsonAsync(jobs);
            return response;
        }

        public class Job
        {
            public string jobId { get; set; }
            public string title { get; set; }
            public string description { get; set; }
        }
    }
}
