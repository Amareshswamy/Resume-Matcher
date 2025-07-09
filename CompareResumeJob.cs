using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Headers;
using Xceed.Words.NET;
using UglyToad.PdfPig;

public class CompareResumeJobFunction
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;

    public CompareResumeJobFunction(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<CompareResumeJobFunction>();
        _httpClient = new HttpClient();
    }

    [Function("CompareResumeJob")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")]
        HttpRequestData req)
    {
        try
        {
            // 🟢 1. Log raw request body
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation("🟢 Raw request body:\n{0}", requestBody);

            var input = JsonSerializer.Deserialize<InputModel>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (input == null || string.IsNullOrWhiteSpace(input.ResumeBlobUrl) || string.IsNullOrWhiteSpace(input.JobDescriptionBlobUrl))
            {
                var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid or missing blob URLs.");
                return badResponse;
            }

            _logger.LogInformation("🟢 Deserialized Input:\nResumeUrl: {0}\nJobDescriptionUrl: {1}", input.ResumeBlobUrl, input.JobDescriptionBlobUrl);

            // 🟢 2. Download Job Description
            var jdBlob = new BlobClient(new Uri(input.JobDescriptionBlobUrl));
            var jdDownload = await jdBlob.DownloadContentAsync();
            string jdText = Encoding.UTF8.GetString(jdDownload.Value.Content.ToArray());
            _logger.LogInformation("🟢 Downloaded JD text (first 500 chars):\n{0}", jdText.Substring(0, Math.Min(500, jdText.Length)));

            // 🟢 3. Download Resume
            var resumeBlob = new BlobClient(new Uri(input.ResumeBlobUrl));
            var resumeDownload = await resumeBlob.DownloadContentAsync();
            var resumeBytes = resumeDownload.Value.Content.ToArray();

            var extension = Path.GetExtension(new Uri(input.ResumeBlobUrl).AbsolutePath).ToLowerInvariant();

            string resumeText = extension switch
            {
                ".pdf" => ExtractPdfText(resumeBytes),
                ".docx" => ExtractDocxText(resumeBytes),
                ".doc" => throw new NotSupportedException("DOC format is not supported. Please use DOCX."),
                _ => Encoding.UTF8.GetString(resumeBytes)
            };
            _logger.LogInformation("🟢 Extracted Resume text (first 500 chars):\n{0}", resumeText.Substring(0, Math.Min(500, resumeText.Length)));

            // 🟢 4. Build prompt
            var prompt = @$"
Compare this resume to the job description. Return ONLY this format:

Matching Score: <number between 0 and 100>%
Matched Keywords: <comma-separated keywords>

Rules:
- Ignore case when comparing keywords.
- Count a keyword as matched if it appears in both the resume and the job description, even once.
- Do not count duplicates more than once.

Resume:
====
{resumeText}

Job Description:
====
{jdText}
";
            _logger.LogInformation("🟢 Prompt text (first 1000 chars):\n{0}", prompt.Substring(0, Math.Min(1000, prompt.Length)));

            // 🟢 5. Build JSON payload
            var requestPayload = new
            {
                messages = new[]
                {
                    new { role = "system", content = "You analyze resumes vs job descriptions. Output only as instructed." },
                    new { role = "user", content = prompt }
                }
            };
            string jsonPayload = JsonSerializer.Serialize(requestPayload);
            _logger.LogInformation("🟢 JSON payload:\n{0}", jsonPayload);

            // 🟢 6. Send to OpenAI
            string openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            string openAiEndpoint = Environment.GetEnvironmentVariable("OPENAI_API_ENDPOINT");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAiKey);

            var response = await _httpClient.PostAsync(
                openAiEndpoint,
                new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            );

            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("🟢 Raw OpenAI response:\n{0}", json);

            // 🟢 7. Parse result text
            using var doc = JsonDocument.Parse(json);
            var resultText = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "(no content)";
            _logger.LogInformation("🟢 Parsed result text:\n{0}", resultText);

            // 🟢 8. Extract Matching Score and Matched Keywords
            int matchingScore = 0;
            string[] matchedKeywords = Array.Empty<string>();

            try
            {
                var lines = resultText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWith("Matching Score:", StringComparison.OrdinalIgnoreCase))
                    {
                        var percentPart = line.Split(':')[1].Trim().TrimEnd('%');
                        if (int.TryParse(percentPart, out int score))
                            matchingScore = score;
                    }
                    else if (line.StartsWith("Matched Keywords:", StringComparison.OrdinalIgnoreCase))
                    {
                        var keywordsPart = line.Split(':', 2)[1].Trim();
                        matchedKeywords = keywordsPart.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                      .Select(k => k.Trim())
                                                      .ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing result text.");
            }

            // 🟢 9. Build JSON response
            var jsonResponse = new
            {
                matchingScore = matchingScore,
                matchedKeywords = matchedKeywords
            };

            var okResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
            okResponse.Headers.Add("Content-Type", "application/json");
            await okResponse.WriteStringAsync(JsonSerializer.Serialize(jsonResponse));
            return okResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request.");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred: " + ex.Message);
            return errorResponse;
        }
    }

    static string ExtractPdfText(byte[] bytes)
    {
        using var pdfStream = new MemoryStream(bytes);
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(pdfStream);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }

    static string ExtractDocxText(byte[] bytes)
    {
        using var docxStream = new MemoryStream(bytes);
        using var doc = DocX.Load(docxStream);
        return doc.Text;
    }

    public class InputModel
    {
        [JsonPropertyName("ResumeUrl")]
        public string ResumeBlobUrl { get; set; }

        [JsonPropertyName("JobDescriptionUrl")]
        public string JobDescriptionBlobUrl { get; set; }
    }
}
