using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Text.Json;

public class AddJob
{
    [Function("AddJob")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        string body = await new StreamReader(req.Body).ReadToEndAsync();
        var job = JsonSerializer.Deserialize<Job>(body);

        if (job == null || string.IsNullOrEmpty(job.jobId))
        {
            var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("Invalid job data.");
            return badResponse;
        }

        string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        string containerName = "jobs";
        var blobClient = new BlobContainerClient(connectionString, containerName);
        await blobClient.CreateIfNotExistsAsync();

        var blob = blobClient.GetBlobClient($"{job.jobId}.json");
        using var stream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(job));
        await blob.UploadAsync(stream, overwrite: true);

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteStringAsync("Job added.");
        return response;
    }

    public record Job(string jobId, string title, string description);
}
