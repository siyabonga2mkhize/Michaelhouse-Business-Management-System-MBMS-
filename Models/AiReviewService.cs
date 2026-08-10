using Michaelhouse.Models;
using Michaelhouse.Controllers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

        // ─── Review a DriverApplication (reads DriverDocument entries) ───────────
       /* public async Task<(string Summary, string Recommendation)> ReviewDriverApplicationAsync(DriverApplication driverApp)
        {
            var extractedDocs = new List<string>();

            var docClient = new DocumentAnalysisClient(
                new Uri(_docEndpoint),
                new AzureKeyCredential(_docKey));

            foreach (var doc in driverApp.Documents ?? new List<DriverDocument>())
            {
                try
                {
                    // Normalize file path (stored as "/Uploads/xxx")
                    var relative = doc.FilePath?.TrimStart('~', '/', '\\') ?? string.Empty;
                    var fullPath = Path.Combine(_uploadRoot, relative);

                    if (!File.Exists(fullPath))
                    {
                        extractedDocs.Add($"=== {doc.DocumentType} ({Path.GetFileName(doc.FilePath)}) — FILE NOT FOUND ===");
                        continue;
                    }

                    using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
                    {
                        var operation = await docClient.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-read", stream);
                        var result = operation.Value;
                        var text = string.Join(" ",
                            result.Pages
                                  .SelectMany(p => p.Lines)
                                  .Select(l => l.Content));

                        extractedDocs.Add($"=== {doc.DocumentType} ({Path.GetFileName(doc.FilePath)}) ===\n{text}");
                    }
                }
                catch (Exception ex)
                {
                    extractedDocs.Add($"=== {doc.DocumentType} ({Path.GetFileName(doc.FilePath)}) — READ ERROR: {ex.Message} ===");
                }
            }

            // Build a driver-specific prompt (concise)
            var docList = (driverApp.Documents != null && driverApp.Documents.Any())
                ? string.Join("\n", driverApp.Documents.Select(d => $"  - {d.DocumentType}: {Path.GetFileName(d.FilePath)}"))
                : "  None submitted";

            var extractedContent = extractedDocs.Count > 0
                ? string.Join("\n\n", extractedDocs)
                : "No documents could be read.";

            var prompt = $@"You are a transport admin reviewing a driver application for Michaelhouse. Respond ONLY with valid JSON — no markdown, no code fences, no preamble.

DRIVER APPLICATION:
- Full Name      : {driverApp.FullName}
- ID Number      : {driverApp.IDNumber}
- Phone          : {driverApp.PhoneNumber}
- Email          : {driverApp.Email}
- Licence No.    : {driverApp.LicenceNumber}
- Licence Expiry : {driverApp.LicenceExpiryDate:yyyy-MM-dd}
- HasPDP         : {driverApp.HasPDP}
- Submitted On   : {driverApp.DateSubmitted:yyyy-MM-dd HH:mm}
- Documents Submitted ({driverApp.Documents?.Count ?? 0}):
{docList}

EXTRACTED DOCUMENT CONTENT:
{extractedContent}

TASKS:
1. Verify identity fields across documents.
2. Ensure licence expiry is valid and not expired.
3. Check document completeness: required = ID and Licence.
4. List any red flags.

Respond with JSON:
{{
  ""summary"": ""Short professional summary."",
  ""concerns"": ""Numbered list of concerns or 'None'."",
  ""document_integrity"": ""Assessment of document authenticity/quality."",
  ""recommendation"": ""APPROVE or REJECT or FLAG""
}}";

            return await CallOpenAIAsync(prompt);
        }*/

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

        //public async Task<(string Summary, string Recommendation)> ReviewDriverApplicationAsync(DriverApplication driverApp)
//
// Replace EVERYTHING from the opening brace { to the closing } with
// the version below. The document extraction loop stays identical —
// only the prompt building section changes.
// ════════════════════════════════════════════════════════════════════════

public async Task<(string Summary, string Recommendation)> ReviewDriverApplicationAsync(
    DriverApplication driverApp)
        {
            var extractedDocs = new List<string>();

            var docClient = new DocumentAnalysisClient(
                new Uri(_docEndpoint),
                new AzureKeyCredential(_docKey));

            foreach (var doc in driverApp.Documents ?? new List<DriverDocument>())
            {
                try
                {
                    var relative = doc.FilePath?.TrimStart('~', '/', '\\') ?? string.Empty;
                    var fullPath = Path.Combine(_uploadRoot, relative);

                    if (!File.Exists(fullPath))
                    {
                        extractedDocs.Add(
                            $"=== {doc.DocumentType} ({Path.GetFileName(doc.FilePath)}) — FILE NOT FOUND ===");
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

                        extractedDocs.Add(
                            $"=== {doc.DocumentType} ({Path.GetFileName(doc.FilePath)}) ===\n{text}");
                    }
                }
                catch (Exception ex)
                {
                    extractedDocs.Add(
                        $"=== {doc.DocumentType} ({Path.GetFileName(doc.FilePath)}) " +
                        $"— READ ERROR: {ex.Message} ===");
                }
            }

            // ── Build document list summary ───────────────────────────────────────────
            var docList = (driverApp.Documents != null && driverApp.Documents.Any())
                ? string.Join("\n", driverApp.Documents
                    .Select(d => $"  - {d.DocumentType}: {Path.GetFileName(d.FilePath)}" +
                                 (d.DocumentType == "Other" && !string.IsNullOrEmpty(d.OtherDocumentType)
                                     ? $" ({d.OtherDocumentType})" : "")))
                : "  None submitted";

            var extractedContent = extractedDocs.Count > 0
                ? string.Join("\n\n", extractedDocs)
                : "No documents could be read.";

            // ── Compute licence validity context ──────────────────────────────────────
            string licenceValidityContext;
            if (driverApp.LicenceExpiryDate==DateTime.MinValue)
            {
                licenceValidityContext = "No expiry date provided.";
            }
            else
            {
                var days = (driverApp.LicenceExpiryDate - DateTime.Today).TotalDays;
                if (days < 0)
                    licenceValidityContext = $"EXPIRED {Math.Abs((int)days)} days ago " +
                                             $"({driverApp.LicenceExpiryDate:dd MMMM yyyy}).";
                else if (days < 30)
                    licenceValidityContext = $"Expires in {(int)days} days — CRITICAL. Renewal required immediately.";
                else if (days < 90)
                    licenceValidityContext = $"Expires in {(int)days} days — WARNING. Renewal required soon.";
                else if (days < 365)
                    licenceValidityContext = $"Valid. Expires {driverApp.LicenceExpiryDate:dd MMMM yyyy} " +
                                             $"(less than 1 year remaining).";
                else
                    licenceValidityContext = $"Valid. Expires {driverApp.LicenceExpiryDate:dd MMMM yyyy} " +
                                             $"({(int)(days / 365)} year(s) remaining — satisfactory).";
            }

            // ── Build the full prompt ─────────────────────────────────────────────────
            var prompt = $@"You are a senior transport manager reviewing a driver application for Michaelhouse, a prestigious South African boarding school in the KwaZulu-Natal Midlands. You are responsible for the safety of students on school transport. Your review must be thorough, professional, and safety-focused. Respond ONLY with valid JSON — no markdown, no code fences, no preamble.
 
═══════════════════════════════════════════════════════
DRIVER APPLICATION DETAILS
═══════════════════════════════════════════════════════
Full Name          : {driverApp.FullName}
ID Number          : {driverApp.IDNumber}
Phone Number       : {driverApp.PhoneNumber}
Email Address      : {driverApp.Email}
Driver's Licence No: {driverApp.LicenceNumber}
Licence Expiry     : {driverApp.LicenceExpiryDate:yyyy-MM-dd} — {licenceValidityContext}
Has PDP            : {(driverApp.HasPDP ? "YES — Professional Driving Permit confirmed" : "NO — No PDP declared")}
Date Submitted     : {driverApp.DateSubmitted:yyyy-MM-dd HH:mm}
 
Documents Submitted ({driverApp.Documents?.Count ?? 0}):
{docList}
 
═══════════════════════════════════════════════════════
EXTRACTED DOCUMENT CONTENT
═══════════════════════════════════════════════════════
{extractedContent}
 
═══════════════════════════════════════════════════════
MICHAELHOUSE DRIVER REQUIREMENTS
═══════════════════════════════════════════════════════
The following are the MANDATORY and PREFERRED requirements for all Michaelhouse drivers:
 
MANDATORY (failure = REJECT):
  1. Valid South African driver's licence — Code C1 or higher preferred for minibus transport
  2. Licence must NOT be expired
  3. Must submit both a copy of their South African ID document AND their driver's licence
  4. ID Number must be a valid 13-digit South African ID format (YYMMDD GGGGG S A Z)
  5. Name on application must match name on identity documents
 
STRONGLY PREFERRED (failure = FLAG, not reject):
  6. PDP (Professional Driving Permit) — required by South African law for transporting passengers for reward.
     Without a PDP, the driver CANNOT legally transport students. This is a serious concern.
  7. Licence validity of at least 1 year remaining — drivers with less than 6 months are a scheduling risk
  8. Licence should match declared licence number — any discrepancy is a red flag
 
DISQUALIFYING (automatic REJECT):
  9. Expired driver's licence
  10. Fraudulent or tampered documents
  11. Name or ID number mismatch between application and supporting documents
  12. No documents submitted at all
 
═══════════════════════════════════════════════════════
YOUR ANALYSIS TASKS — perform ALL of the following
═══════════════════════════════════════════════════════
 
1. IDENTITY VERIFICATION
   - Does the full name on the application match the name(s) on the submitted ID document?
   - Does the ID number on the application match the ID number visible in the documents?
   - Is the ID number format valid for a South African ID (13 digits)?
   - Flag any spelling discrepancies or mismatches, even minor ones.
 
2. LICENCE VALIDITY & COMPLIANCE
   - Is the driver's licence currently valid (not expired)?
   - How many days/months/years remain before expiry? Is this acceptable for school transport?
   - Does the declared licence number match what appears on the licence document?
   - What licence code is visible? Code C1 or higher is preferred for minibus transport.
   - Is the PDP present? If not, note that South African law (National Road Traffic Act) 
     requires a PDP for transporting passengers for reward — this is a CRITICAL concern 
     for school transport.
 
3. DOCUMENT COMPLETENESS & AUTHENTICITY
   - Required documents: South African ID AND Driver's Licence copy. Are both present?
   - For each document: is the content readable and coherent, or does it appear 
     corrupted, blank, or illegible?
   - Look for signs of tampering: inconsistent fonts, suspicious formatting, 
     data that does not align between the application form and the documents.
   - Are any documents clearly wrong file types, placeholders, or test uploads?
 
4. SAFETY SUITABILITY ASSESSMENT
   - Based on all available information, assess whether this applicant appears suitable 
     to transport school children safely.
   - Consider: is the licence appropriate for passenger transport?
   - Consider: is there any information suggesting the applicant may be a safety risk?
 
5. CROSS-CHECK CONSISTENCY
   - Do all details across documents (name, ID number, licence number, dates) align?
   - Are there any inconsistencies between what the applicant declared and what 
     appears in the documents?
 
6. RED FLAGS
   - List every specific concern found.
   - Be explicit and precise. Do not list vague concerns.
   - Prioritise safety-relevant issues above administrative ones.
 
═══════════════════════════════════════════════════════
DECISION CRITERIA
═══════════════════════════════════════════════════════
APPROVE : All mandatory requirements met. Identity verified. Licence valid and not 
          close to expiry. Documents complete and authentic. PDP present or 
          confirmed not legally required in this context. No red flags.
 
FLAG    : Mostly compliant but has minor issues requiring human review. Examples:
          - No PDP declared (serious but not automatic reject — transport manager must decide)
          - Licence expiring within 6 months
          - Minor document quality issues
          - Name spelling slightly different (could be transcription error)
          - Missing one required document but other is present
 
REJECT  : One or more disqualifying issues present. Examples:
          - Expired driver's licence
          - No documents submitted
          - Clear identity mismatch (name or ID number)
          - Evidence of document tampering
          - ID number does not match valid South African format
 
Respond ONLY with this exact JSON structure (no markdown, no extra text, no code fences):
{{
  ""summary"": ""3-5 sentence professional overview of the applicant, their documents, licence status, PDP status, and overall suitability for school transport driving."",
  ""identity_verification"": ""Assessment of whether the applicant's identity has been verified across their submitted documents, or whether discrepancies exist."",
  ""licence_assessment"": ""Detailed assessment of the driver's licence: validity period remaining, licence code if visible, whether it is suitable for passenger transport, and PDP status with legal implications."",
  ""document_integrity"": ""Assessment of whether all required documents were submitted, whether they appear authentic and readable, and any concerns about document quality or tampering."",
  ""safety_suitability"": ""Assessment of the applicant's overall suitability to safely transport Michaelhouse students, based on all available information."",
  ""concerns"": ""Numbered list of specific concerns found (e.g. '1. No PDP declared — legally required for passenger transport. 2. Licence expires in 45 days.'), or the word None if no concerns exist."",
  ""recommendation"": ""APPROVE or REJECT or FLAG""
}}";

            return await CallOpenAIAsync(prompt);
        }
        // ─── OpenAI Call for Drivers ─────────────────────────────────────────────
        private async Task<(string Summary, string Recommendation)> CallOpenAIForDriverAsync(string prompt)
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
                    content = "You are a transport safety review assistant. You must respond with a single valid JSON object only. Do not include markdown code fences, backticks, or any text outside the JSON object."
                },
                new { role = "user", content = prompt }
            },
                    max_tokens = 1500, // Increased to accommodate the more detailed response
                    temperature = 0.1
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
                    var envelope = JObject.Parse(responseJson);
                    var rawContent = (string)envelope.SelectToken("choices[0].message.content");

                    if (string.IsNullOrWhiteSpace(rawContent))
                        return ("AI returned an empty response.", "FLAG");

                    rawContent = StripMarkdownFences(rawContent).Trim();
                    var aiResult = JObject.Parse(rawContent);

                    // Extract the new specific driver fields
                    var summary = aiResult.Value<string>("summary") ?? "No summary provided.";
                    var identity = aiResult.Value<string>("identity_verification") ?? "";
                    var licence = aiResult.Value<string>("licence_assessment") ?? "";
                    var docIntegrity = aiResult.Value<string>("document_integrity") ?? "";
                    var safety = aiResult.Value<string>("safety_suitability") ?? "";
                    var concerns = aiResult.Value<string>("concerns") ?? "None";
                    var recommendation = (aiResult.Value<string>("recommendation") ?? "FLAG").ToUpper().Trim();

                    if (recommendation != "APPROVE" && recommendation != "REJECT" && recommendation != "FLAG")
                        recommendation = "FLAG";

                    // Build the formatted string for your UI
                    var sb = new StringBuilder();
                    sb.AppendLine(summary);

                    if (!string.IsNullOrWhiteSpace(identity))
                    {
                        sb.AppendLine("\n--- Identity Verification ---");
                        sb.AppendLine(identity);
                    }

                    if (!string.IsNullOrWhiteSpace(licence))
                    {
                        sb.AppendLine("\n--- Licence & Compliance ---");
                        sb.AppendLine(licence);
                    }

                    if (!string.IsNullOrWhiteSpace(docIntegrity))
                    {
                        sb.AppendLine("\n--- Document Integrity ---");
                        sb.AppendLine(docIntegrity);
                    }

                    if (!string.IsNullOrWhiteSpace(safety))
                    {
                        sb.AppendLine("\n--- Safety Suitability ---");
                        sb.AppendLine(safety);
                    }

                    if (!string.IsNullOrWhiteSpace(concerns) && concerns.ToLower() != "none")
                    {
                        sb.AppendLine("\n--- Concerns / Red Flags ---");
                        sb.AppendLine(concerns);
                    }

                    return (sb.ToString().Trim(), recommendation);
                }
                catch (Exception ex)
                {
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