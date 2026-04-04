using Michaelhouse.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;

namespace Michaelhouse.Services
{
    public class AiReviewService
    {
        private readonly string _docEndpoint;
        private readonly string _docKey;
        private readonly string _openAiKey;
        private readonly string _uploadRoot;

        public AiReviewService()
        {
            _docEndpoint = ConfigurationManager.AppSettings["AzureDocIntelligence:Endpoint"];
            _docKey = ConfigurationManager.AppSettings["AzureDocIntelligence:Key"];
            _openAiKey = ConfigurationManager.AppSettings["OpenAI:Key"];

            var relativePath = ConfigurationManager.AppSettings["DocumentStorage:UploadRoot"] ?? "~/App_Data/Uploads";
            _uploadRoot = relativePath.StartsWith("~")
                ? System.Web.Hosting.HostingEnvironment.MapPath(relativePath)
                : relativePath;
        }

        // ─── Main Entry Point ─────────────────────────────────────────────────────
        /// <summary>
        /// Step 1: Reads each uploaded document using Azure Document Intelligence.
        /// Step 2: Sends extracted text + application details to OpenAI.
        /// Returns (summary, recommendation) where recommendation is APPROVE | REJECT | FLAG.
        /// </summary>
        public async Task<(string Summary, string Recommendation)> ReviewApplicationAsync(Application application)
        {
            var extractedDocs = new List<string>();

            var docClient = new DocumentAnalysisClient(
                new Uri(_docEndpoint),
                new AzureKeyCredential(_docKey));

            // Read each document
            foreach (var doc in application.Documents ?? new List<Document>())
            {
                try
                {
                    var fullPath = Path.Combine(_uploadRoot, doc.FilePath);
                    if (!File.Exists(fullPath))
                    {
                        extractedDocs.Add($"=== {doc.Type} ({doc.FileName}) — FILE NOT FOUND ===");
                        continue;
                    }

                    using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
                    {
                        var operation = await docClient.AnalyzeDocumentAsync(
                            WaitUntil.Completed, "prebuilt-read", stream);

                        var result = operation.Value;
                        var text = string.Join(" ",
                            result.Pages
                                  .SelectMany(p => p.Lines)
                                  .Select(l => l.Content));

                        extractedDocs.Add($"=== {doc.Type} ({doc.FileName}) ===\n{text}");
                    }
                }
                catch (Exception ex)
                {
                    extractedDocs.Add($"=== {doc.Type} ({doc.FileName}) — READ ERROR: {ex.Message} ===");
                }
            }

            // Send to OpenAI
            var prompt = BuildPrompt(application, extractedDocs);
            return await CallOpenAIAsync(prompt);
        }

        // ─── Prompt Builder ───────────────────────────────────────────────────────
        private string BuildPrompt(Application app, List<string> extractedDocs)
        {
            int age = 0;
            if (app.Student?.DOB != null)
            {
                age = DateTime.Today.Year - app.Student.DOB.Year;
                if (app.Student.DOB.Date > DateTime.Today.AddYears(-age)) age--;
            }

            var docList = (app.Documents != null && app.Documents.Any())
                ? string.Join("\n", app.Documents.Select(d => $"  - {d.Type}: {d.FileName}"))
                : "  None submitted";

            var extractedContent = extractedDocs.Count > 0
                ? string.Join("\n\n", extractedDocs)
                : "No documents could be read.";

            return $@"You are reviewing a school enrollment application. Analyze carefully and respond ONLY in valid JSON.
 
APPLICATION DETAILS:
- Student Name  : {app.Student?.Name ?? "Unknown"}
- Date of Birth : {app.Student?.DOB:yyyy-MM-dd} (Age: {age})
- Parent/Guardian: {app.Parent?.Name ?? "Unknown"}
- Parent Contact : {app.Parent?.Contact ?? "Unknown"}
- Application Year: {app.ApplicationYear}
- Submitted On  : {app.Date:yyyy-MM-dd HH:mm}
- Documents Submitted ({app.Documents?.Count ?? 0}):
{docList}
 
EXTRACTED DOCUMENT CONTENT:
{extractedContent}
 
YOUR TASK:
1. Verify the student age is appropriate for the application year (typically 5–18 years).
2. Confirm required documents are present and readable — Birth Certificate and Report Card are mandatory.
3. Check name consistency across application and documents.
4. List any concerns, missing info, or inconsistencies.
5. Give a clear recommendation.
 
Respond ONLY with this exact JSON (no markdown, no extra text):
{{
  ""summary"": ""2-3 sentence overview of the student and documents"",
  ""concerns"": ""Bullet list of issues found, or 'None' if all looks good"",
  ""recommendation"": ""APPROVE or REJECT or FLAG""
}}
 
Use FLAG when documents are incomplete, unreadable, or need human judgement.
Use REJECT only when there is a clear disqualifying issue (wrong age, fraudulent docs).
Use APPROVE when everything is complete and correct.";
        }

        // ─── OpenAI Call ─────────────────────────────────────────────────────────
        private async Task<(string Summary, string Recommendation)> CallOpenAIAsync(string prompt)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_openAiKey}");

                var requestBody = new
                {
                    model = "gpt-4o-mini",
                    messages = new[]
                    {
                        new { role = "system", content = "You are a school enrollment review assistant. Always respond with valid JSON only. No markdown, no preamble." },
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 800,
                    temperature = 0.2
                };

                var json = JsonConvert.SerializeObject(requestBody);

                HttpResponseMessage response;
                try
                {
                    response = await client.PostAsync(
                        "https://api.openai.com/v1/chat/completions",
                        new StringContent(json, Encoding.UTF8, "application/json")
                    );
                }
                catch (Exception ex)
                {
                    return ($"AI service could not be reached: {ex.Message}", "FLAG");
                }

                var responseJson = await response.Content.ReadAsStringAsync();

                try
                {
                    using (var doc = JsonDocument.Parse(responseJson))
                    {
                        var text = doc.RootElement
                            .GetProperty("choices")[0]
                            .GetProperty("message")
                            .GetProperty("content")
                            .GetString();

                        text = text?.Replace("```json", "").Replace("```", "").Trim();

                        using (var aiDoc = JsonDocument.Parse(text))
                        {
                            var summary = aiDoc.RootElement.TryGetProperty("summary", out var s) ? s.GetString() : "No summary provided.";
                            var concerns = aiDoc.RootElement.TryGetProperty("concerns", out var c) ? c.GetString() : "None";
                            var recommendation = aiDoc.RootElement.TryGetProperty("recommendation", out var r) ? r.GetString()?.ToUpper() : "FLAG";

                            var fullSummary = $"{summary}\n\nConcerns:\n{concerns}";
                            return (fullSummary, recommendation ?? "FLAG");
                        }
                    }
                }
                catch (Exception ex)
                {
                    return ($"AI response could not be parsed.\nRaw response: {responseJson}\nError: {ex.Message}", "FLAG");
                }
            }
        }
    }
}