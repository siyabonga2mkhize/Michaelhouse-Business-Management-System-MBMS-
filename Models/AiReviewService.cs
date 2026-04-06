using Michaelhouse.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
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
        public async Task<(string Summary, string Recommendation)> ReviewApplicationAsync(Application application)
        {
            var extractedDocs = new List<string>();

            var docClient = new DocumentAnalysisClient(
                new Uri(_docEndpoint),
                new AzureKeyCredential(_docKey));

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

            return $@"You are a senior school admissions officer reviewing an enrollment application for Michaelhouse, a prestigious boys' boarding school. Your review must be thorough and analytical. Respond ONLY with valid JSON — no markdown, no code fences, no preamble.

APPLICATION DETAILS:
- Student Name   : {app.Student?.Name ?? "Unknown"}
- Date of Birth  : {app.Student?.DOB:yyyy-MM-dd} (Age: {age})
- Parent/Guardian: {app.Parent?.Name ?? "Unknown"}
- Parent Contact : {app.Parent?.Contact ?? "Unknown"}
- Application Year: {app.ApplicationYear}
- Submitted On   : {app.Date:yyyy-MM-dd HH:mm}
- Documents Submitted ({app.Documents?.Count ?? 0}):
{docList}

EXTRACTED DOCUMENT CONTENT:
{extractedContent}

YOUR ANALYSIS TASKS — perform ALL of the following:

1. IDENTITY VERIFICATION
   - Does the student name on the application match the name(s) on submitted documents?
   - Does the date of birth on the application match what appears in the birth certificate?
   - Flag any spelling discrepancies or mismatches, even minor ones.

2. AGE & ELIGIBILITY CHECK
   - Is the student's age appropriate for the applied year? (Michaelhouse accepts students aged 13–18.)
   - Would the student be the correct age at the start of the {app.ApplicationYear} academic year?

3. DOCUMENT COMPLETENESS & AUTHENTICITY
   - Required documents: Birth Certificate AND most recent Academic Report Card. Are both present?
   - For each document: is the content readable and coherent, or does it appear corrupted/blank?
   - Look for signs of tampering: inconsistent fonts, suspicious formatting, data that does not align.
   - Are there any documents that appear to be placeholders, test files, or clearly wrong file types?

4. ACADEMIC PERFORMANCE ANALYSIS (from Report Card if present)
   - Summarise the student's academic standing: strong, average, or below expectations.
   - Note any specific subject strengths or weaknesses visible in the extracted text.
   - Flag any repeated grades, incomplete terms, or unusual patterns.

5. CONSISTENCY CROSS-CHECK
   - Do the details across all documents (name, DOB, school name, grade level) align consistently?
   - Does the application year make sense given the student's current grade?

6. RED FLAGS
   - List any specific concerns that would warrant human review or rejection.
   - Be explicit: vague concerns should not be listed unless they are genuinely notable.

Respond ONLY with this exact JSON structure (no markdown, no extra text, no code fences):
{{
  ""summary"": ""3-4 sentence professional overview of the student, their documents, and overall application quality."",
  ""concerns"": ""Numbered list of specific concerns found (e.g. 1. Name mismatch between application and birth certificate. 2. Report card is missing.), or the word None if everything is in order."",
  ""academic_standing"": ""Brief summary of academic performance from documents, or 'No report card submitted' if absent."",
  ""document_integrity"": ""Assessment of whether documents appear authentic, complete, and consistent."",
  ""recommendation"": ""APPROVE or REJECT or FLAG""
}}

Decision criteria:
- APPROVE : All required documents present, identity verified, age correct, no red flags.
- FLAG    : Minor issues, missing report card, unreadable content, or anything needing human review.
- REJECT  : Clear disqualifying issue — wrong age bracket, fraudulent-looking documents, critical identity mismatch.";
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
                        new
                        {
                            role    = "system",
                            content = "You are a school admissions review assistant. You must respond with a single valid JSON object only. Do not include markdown code fences, backticks, or any text outside the JSON object."
                        },
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 1200,
                    temperature = 0.1   // Lower = more deterministic / consistent JSON output
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
                    // ── Step 1: Parse the outer OpenAI envelope with Newtonsoft ──────────
                    // Newtonsoft correctly handles all escape sequences in the content string.
                    var envelope = JObject.Parse(responseJson);
                    var rawContent = (string)envelope
                        .SelectToken("choices[0].message.content");

                    if (string.IsNullOrWhiteSpace(rawContent))
                        return ("AI returned an empty response.", "FLAG");

                    // ── Step 2: Strip markdown fences if the model ignored instructions ──
                    rawContent = StripMarkdownFences(rawContent).Trim();

                    // ── Step 3: Parse the inner AI JSON with Newtonsoft ──────────────────
                    var aiResult = JObject.Parse(rawContent);

                    var summary = aiResult.Value<string>("summary") ?? "No summary provided.";
                    var concerns = aiResult.Value<string>("concerns") ?? "None";
                    var academicStanding = aiResult.Value<string>("academic_standing") ?? "";
                    var docIntegrity = aiResult.Value<string>("document_integrity") ?? "";
                    var recommendation = (aiResult.Value<string>("recommendation") ?? "FLAG").ToUpper().Trim();

                    // Validate recommendation value
                    if (recommendation != "APPROVE" && recommendation != "REJECT" && recommendation != "FLAG")
                        recommendation = "FLAG";

                    // Build the full summary displayed in the UI
                    var sb = new StringBuilder();
                    sb.AppendLine(summary);

                    if (!string.IsNullOrWhiteSpace(academicStanding))
                    {
                        sb.AppendLine();
                        sb.AppendLine("Academic Standing:");
                        sb.AppendLine(academicStanding);
                    }

                    if (!string.IsNullOrWhiteSpace(docIntegrity))
                    {
                        sb.AppendLine();
                        sb.AppendLine("Document Integrity:");
                        sb.AppendLine(docIntegrity);
                    }

                    if (!string.IsNullOrWhiteSpace(concerns) && concerns.ToLower() != "none")
                    {
                        sb.AppendLine();
                        sb.AppendLine("Concerns:");
                        sb.AppendLine(concerns);
                    }

                    return (sb.ToString().Trim(), recommendation);
                }
                catch (Exception ex)
                {
                    // Return the raw response so the developer can debug — but truncate it
                    var truncated = responseJson.Length > 300
                        ? responseJson.Substring(0, 300) + "…"
                        : responseJson;

                    return ($"AI response could not be parsed. Error: {ex.Message}\n\nRaw (truncated):\n{truncated}", "FLAG");
                }
            }
        }

        // ─── Helper: Strip markdown code fences ──────────────────────────────────
        private static string StripMarkdownFences(string text)
        {
            // Remove ```json ... ``` or ``` ... ``` wrappers
            var pattern = @"^```(?:json)?\s*([\s\S]*?)\s*```$";
            var match = Regex.Match(text.Trim(), pattern, RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value : text;
        }
    }
}