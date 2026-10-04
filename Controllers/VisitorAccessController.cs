using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Web.Mvc;
using Michaelhouse.Models;
using Newtonsoft.Json;
using QRCoder;

namespace Michaelhouse.Controllers
{
    public class VisitorAccessController : Controller
    {
        private readonly DBContextClass db = new DBContextClass();
        private static readonly string QrSecretSalt = "MH_Secure_GatePass_Salt_2026";

        // ============================================================
        // 📝 1. PUBLIC VISITOR REQUEST FORM (Submitted by Visitor/Parent)
        // ============================================================

        [HttpGet]
        [AllowAnonymous]
        public ActionResult Create()
        {
            var model = new VisitorAccessRequest
            {
                VisitDate = DateTime.Today.AddDays(1),
                StartTime = new TimeSpan(14, 0, 0),
                EndTime = new TimeSpan(16, 0, 0)
            };
            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Create(VisitorAccessRequest model, string studentSearch)
        {
            ViewBag.StudentSearch = studentSearch;

            if (string.IsNullOrWhiteSpace(studentSearch))
            {
                ModelState.AddModelError("", "Please enter the host student's name.");
                return View(model);
            }

            // Locate Host Student strictly by name matching
            string cleanName = studentSearch.Trim();
            var student = db.Students.ToList().FirstOrDefault(s =>
                $"{s.FirstName} {s.LastName}".Equals(cleanName, StringComparison.OrdinalIgnoreCase) ||
                s.FirstName.Equals(cleanName, StringComparison.OrdinalIgnoreCase) ||
                s.LastName.Equals(cleanName, StringComparison.OrdinalIgnoreCase) ||
                (s.FirstName + " " + s.LastName).IndexOf(cleanName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (student == null)
            {
                ModelState.AddModelError("", $"Host student '{studentSearch}' could not be located in the Michaelhouse registry.");
                return View(model);
            }

            model.StudentId = student.StudentId;

            // Ensure BoardingHouseName is set
            if (string.IsNullOrWhiteSpace(model.BoardingHouseName))
            {
                if (student.ResidenceId.HasValue)
                {
                    var residence = db.Residences.Find(student.ResidenceId.Value);
                    model.BoardingHouseName = residence?.Name ?? "Main Campus";
                }
                else
                {
                    model.BoardingHouseName = "Main Campus";
                }
            }

            // Schedule Validation
            if (model.StartTime >= model.EndTime)
            {
                ModelState.AddModelError("", "Invalid Schedule: Start Time must precede End Time.");
                return View(model);
            }

            // Closed Weekend Policy Check (Auto-Reject Rule)
            bool isClosedWeekend = db.TermCalendars.Any(c =>
                c.IsClosedWeekend &&
                model.VisitDate >= c.StartDate &&
                model.VisitDate <= c.EndDate);

            if (isClosedWeekend)
            {
                ModelState.AddModelError("", "Access Denied: The requested date falls on an official Closed Weekend.");
                return View(model);
            }

            // ============================================================
            // 🏛️ DYNAMIC DATABASE RULE EVALUATION ENGINE
            // ============================================================
            var activeRules = db.CampusRules.Where(r => r.IsActive).ToList();
            List<string> detectedConflicts = new List<string>();

            foreach (var rule in activeRules)
            {
                // Overlap calculation: Request starts before rule ends AND request ends after rule starts
                bool overlaps = model.StartTime < rule.EndTime && model.EndTime > rule.StartTime;

                if (overlaps)
                {
                    // Strict block rule (e.g., Night Curfew) -> Reject submission immediately
                    if (rule.IsStrictBlock)
                    {
                        ModelState.AddModelError("", $"Access Denied: Requested window conflicts with {rule.RuleName} ({rule.StartTime:hh\\:mm} - {rule.EndTime:hh\\:mm}).");
                        return View(model);
                    }

                    // Soft conflict rule -> Append flag for Housemaster consideration
                    detectedConflicts.Add($"{rule.RuleName} ({rule.StartTime:hh\\:mm} - {rule.EndTime:hh\\:mm})");
                }
            }

            // Store policy evaluation status on the request model
            if (detectedConflicts.Any())
            {
                model.HasPolicyConflict = true;
                model.PolicyFlagDetails = "Warning: Overlaps with " + string.Join(", ", detectedConflicts);
            }
            else
            {
                model.HasPolicyConflict = false;
                model.PolicyFlagDetails = "Complies with all campus schedule rules.";
            }

            // Safeguarding & Zone Enforcement Rules
            model.FinalAssignedZone = model.RequestedZone;
            string zoneMessage = string.Empty;

            if (!model.IsParentOrGuardian && model.RequestedZone == VisitorAccessZone.HouseCommonRoom)
            {
                model.FinalAssignedZone = VisitorAccessZone.PublicCampusGrounds;
                zoneMessage = " (Zone adjusted to Public Campus Grounds per safeguarding policy)";
            }

            model.AccessGatePassCode = "MH-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            model.CreatedAt = DateTime.Now;

            // Decision Engine: Parent/Guardian Auto-Approval vs Housemaster Review Queue
            if (model.IsParentOrGuardian)
            {
                model.Status = VisitorRequestStatus.AutoApprovedWeekend;
                db.VisitorAccessRequests.Add(model);
                db.SaveChanges();

                SendVisitorPassEmail(model);
                NotifyStudent(student, model, "Your parent/guardian has registered a visit.");

                TempData["Success"] = $"Request Approved automatically. Access Pass sent to {model.VisitorEmail}.{zoneMessage}";
                return RedirectToAction("RequestConfirmation");
            }
            else
            {
                model.Status = VisitorRequestStatus.PendingHousemaster;
                db.VisitorAccessRequests.Add(model);
                db.SaveChanges();

                NotifyStudent(student, model, "A visitor request has been submitted for you and sent to your Housemaster for approval.");

                TempData["Success"] = $"Request submitted successfully. Pending Housemaster review.{zoneMessage}";
                return RedirectToAction("RequestConfirmation");
            }
        }

        [AllowAnonymous]
        public ActionResult RequestConfirmation()
        {
            return View();
        }

        // ============================================================
        // 📋 2. STUDENT DASHBOARD (Read-Only Incoming Visits)
        // ============================================================

        [HttpGet]
        public ActionResult MyRequests()
        {
            var userId = (int?)Session["UserId"];
            var student = db.Students.FirstOrDefault(s => s.UserId == userId);
            if (student == null) return RedirectToAction("Login", "Account");

            var requests = db.VisitorAccessRequests
                .Where(r => r.StudentId == student.StudentId)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            return View(requests);
        }

        // ============================================================
        // 🏫 3. HOUSEMASTER DASHBOARD & REVIEW QUEUE
        // ============================================================

        [HttpGet]
        [AllowAnonymous]
        public ActionResult HousemasterQueue()
        {
            var pendingRequests = db.VisitorAccessRequests
                .Where(r => r.Status == VisitorRequestStatus.PendingHousemaster)
                .OrderBy(r => r.VisitDate)
                .ThenBy(r => r.StartTime)
                .ToList();

            return View(pendingRequests);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ReviewRequest(int id, string action, string remarks)
        {
            var request = db.VisitorAccessRequests.Find(id);
            if (request == null) return HttpNotFound();

            var student = db.Students.Find(request.StudentId);

            if (string.Equals(action, "Approve", StringComparison.OrdinalIgnoreCase))
            {
                request.Status = VisitorRequestStatus.Approved;
                db.SaveChanges();

                SendVisitorPassEmail(request);
                if (student != null)
                {
                    NotifyStudent(student, request, $"Housemaster approved visit request for {request.VisitorFullName}.");
                }
                TempData["Success"] = $"Approved visit for {request.VisitorFullName}. Gate Pass issued.";
            }
            else if (string.Equals(action, "Reject", StringComparison.OrdinalIgnoreCase))
            {
                request.Status = VisitorRequestStatus.RejectedPolicyViolation;
                request.AccessGatePassCode = null;
                db.SaveChanges();

                if (student != null)
                {
                    NotifyStudent(student, request, $"Housemaster declined visit request for {request.VisitorFullName}.");
                }
                TempData["Error"] = $"Rejected visit request for {request.VisitorFullName}.";
            }

            request.ActionedByHousemasterId = (int?)Session["UserId"] ?? 999;
            request.ActionedAt = DateTime.Now;
            request.HousemasterRemarks = remarks ?? "Actioned via Housemaster Approval Queue.";
            db.SaveChanges();

            return RedirectToAction("HousemasterQueue");
        }

        // ============================================================
        // 🎟️ 4. DIGITAL GATE PASS & SECURITY SCANNING
        // ============================================================

        [HttpGet]
        [AllowAnonymous]
        public ActionResult VisitorPass(int id)
        {
            var request = db.VisitorAccessRequests.Find(id);
            if (request == null || (request.Status != VisitorRequestStatus.Approved && request.Status != VisitorRequestStatus.AutoApprovedWeekend))
                return HttpNotFound();

            return View("VisitorPass", request);
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult ScanGate() => View();

        public class QrPayload
        {
            public int rid { get; set; }
            public long ts { get; set; }
            public string sig { get; set; }
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult GenerateQrImage(int id)
        {
            var request = db.VisitorAccessRequests.Find(id);
            if (request == null || string.IsNullOrEmpty(request.AccessGatePassCode))
                return HttpNotFound();

            long unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string signature = GenerateHmacSignature(request.RequestId, unixTimestamp, request.AccessGatePassCode);

            var payload = new QrPayload { rid = request.RequestId, ts = unixTimestamp, sig = signature };
            string jsonPayload = JsonConvert.SerializeObject(payload);

            using (var generator = new QRCodeGenerator())
            using (var data = generator.CreateQrCode(jsonPayload, QRCodeGenerator.ECCLevel.Q))
            {
                var qrCode = new PngByteQRCode(data);
                return File(qrCode.GetGraphic(40), "image/png");
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult ValidateGatePass(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
                return Json(new { success = false, message = "No payload detected." }, JsonRequestBehavior.AllowGet);

            try
            {
                var payload = JsonConvert.DeserializeObject<QrPayload>(data);
                if (payload == null || payload.rid <= 0 || string.IsNullOrEmpty(payload.sig))
                    return Json(new { success = false, message = "Invalid Pass Structure." }, JsonRequestBehavior.AllowGet);

                // 1. DYNAMIC TOKEN TIMEOUT: Strict 5-Minute (300 seconds) Validity Window
                var nowUtc = DateTime.UtcNow;
                var qrTime = DateTimeOffset.FromUnixTimeSeconds(payload.ts).UtcDateTime;
                var age = nowUtc - qrTime;

                if (age < TimeSpan.FromSeconds(-30) || age > TimeSpan.FromMinutes(5))
                {
                    return Json(new { success = false, message = "QR Pass Expired. Please refresh your gate pass page." }, JsonRequestBehavior.AllowGet);
                }

                var request = db.VisitorAccessRequests.FirstOrDefault(r => r.RequestId == payload.rid);
                if (request == null || string.IsNullOrEmpty(request.AccessGatePassCode))
                    return Json(new { success = false, message = "Unrecognized Gate Pass." }, JsonRequestBehavior.AllowGet);

                // 2. SIGNATURE SECURITY CHECK
                string expectedSignature = GenerateHmacSignature(payload.rid, payload.ts, request.AccessGatePassCode);
                if (!CryptographicEquals(payload.sig, expectedSignature))
                    return Json(new { success = false, message = "Security Check Failed: Invalid Signature." }, JsonRequestBehavior.AllowGet);

                // 3. APPROVAL STATUS CHECK
                if (request.Status != VisitorRequestStatus.Approved && request.Status != VisitorRequestStatus.AutoApprovedWeekend)
                {
                    LogScan(request, "Denied", $"Invalid status: {request.Status}", isEntry: false);
                    return Json(new { success = false, message = $"Access Denied: Request status is {request.Status}." }, JsonRequestBehavior.AllowGet);
                }

                var nowLocal = DateTime.Now;

                // 4. DATE VALIDATION
                if (nowLocal.Date != request.VisitDate.Date)
                {
                    LogScan(request, "Denied", $"Invalid Date: expected {request.VisitDate:dd MMM yyyy}", isEntry: false);
                    return Json(new { success = false, message = $"Pass valid for scheduled date: {request.VisitDate:dd MMM yyyy}." }, JsonRequestBehavior.AllowGet);
                }

                // 5. SCAN STATE ENGINE (Prevent Double-Entry & Enforce Exit Flow)
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);

                var existingEntry = db.VisitorScanLogs.FirstOrDefault(l => l.RequestId == request.RequestId && l.Status == "Granted" && l.IsEntry == true && l.ScannedAt >= today && l.ScannedAt < tomorrow);
                var existingExit = db.VisitorScanLogs.FirstOrDefault(l => l.RequestId == request.RequestId && l.Status == "Exit" && l.IsEntry == false && l.ScannedAt >= today && l.ScannedAt < tomorrow);

                // CASE A: Visitor has already entered and already exited today -> FULL LOCKOUT
                if (existingExit != null)
                {
                    LogScan(request, "Denied", "Attempted scan after complete checkout", isEntry: false);
                    return Json(new { success = false, message = "Access Denied: This pass has already been used for entry and exit today." }, JsonRequestBehavior.AllowGet);
                }

                // CASE B: Visitor has ALREADY scanned for Entry -> FORCE EXIT FLOW
                if (existingEntry != null)
                {
                    LogScan(request, "Exit", "Visitor exited campus", isEntry: false);

                    var duration = nowLocal - existingEntry.ScannedAt;
                    string formattedDuration = $"{duration.Hours:D2}h {duration.Minutes:D2}m";

                    return Json(new
                    {
                        success = true,
                        isExit = true,
                        message = $"Departure logged for {request.VisitorFullName}. Have a safe journey!",
                        visitorName = request.VisitorFullName,
                        assignedZone = request.FinalAssignedZone.ToString(),
                        visitDuration = formattedDuration
                    }, JsonRequestBehavior.AllowGet);
                }

                // CASE C: First scan of the day -> ENTRY FLOW
                LogScan(request, "Granted", "Access Granted", isEntry: true);

                return Json(new
                {
                    success = true,
                    isExit = false,
                    message = $"Access Granted. Welcome to Michaelhouse, {request.VisitorFullName}!",
                    visitorName = request.VisitorFullName,
                    assignedZone = request.FinalAssignedZone.ToString(),
                    visitDuration = "N/A"
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Validation Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ============================================================
        // 📧 HELPER METHODS & NOTIFICATIONS
        // ============================================================

        private void LogScan(VisitorAccessRequest request, string status, string reason, bool isEntry = true)
        {
            try
            {
                var log = new VisitorScanLog
                {
                    RequestId = request.RequestId,
                    ScannedAt = DateTime.Now,
                    ScannerUserId = (int?)Session["UserId"],
                    ScannerName = Session["UserName"]?.ToString() ?? "Security Gate",
                    Status = status,
                    Reason = reason,
                    Location = "Main Gate",
                    IsEntry = isEntry
                };
                db.VisitorScanLogs.Add(log);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Log error: {ex.Message}");
            }
        }

        private void NotifyStudent(Student student, VisitorAccessRequest request, string message)
        {
            TempData["StudentNotification"] = message;
            System.Diagnostics.Debug.WriteLine($"Notification sent to Student ID {student.StudentId}: {message}");
        }

        private void SendVisitorPassEmail(VisitorAccessRequest request)
        {
            try
            {
                var smtpHost = ConfigurationManager.AppSettings["Email:SmtpHost"];
                var smtpPort = int.TryParse(ConfigurationManager.AppSettings["Email:SmtpPort"], out int p) ? p : 587;
                var smtpUser = ConfigurationManager.AppSettings["Email:Username"];
                var smtpPass = ConfigurationManager.AppSettings["Email:Password"];
                var fromEmail = ConfigurationManager.AppSettings["Email:FromAddress"];
                var fromName = ConfigurationManager.AppSettings["Email:FromName"] ?? "Michaelhouse Security";

                if (string.IsNullOrWhiteSpace(smtpHost))
                {
                    throw new InvalidOperationException("SMTP Host key ('Email:SmtpHost') is missing or empty in web.config.");
                }

                string passLink = $"{Request.Url.Scheme}://{Request.Url.Authority}/VisitorAccess/VisitorPass/{request.RequestId}";

                string body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2 style='color: #C21E2E;'>Michaelhouse Digital Gate Pass</h2>
                    <p>Dear {request.VisitorFullName},</p>
                    <p>Your access request to Michaelhouse has been approved.</p>
                    <hr />
                    <p><strong>Date:</strong> {request.VisitDate:dd MMM yyyy}</p>
                    <p><strong>Time Window:</strong> {request.StartTime} - {request.EndTime}</p>
                    <p><strong>Approved Zone:</strong> {request.FinalAssignedZone}</p>
                    <hr />
                    <p><a href='{passLink}' style='padding: 12px 24px; background-color: #C21E2E; color: #fff; text-decoration: none; font-weight: bold;'>Open Gate Pass</a></p>
                </body>
                </html>";

                using (var client = new SmtpClient(smtpHost, smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(smtpUser, smtpPass);

                    var msg = new MailMessage
                    {
                        From = new MailAddress(fromEmail, fromName),
                        Subject = "Michaelhouse Visitor Gate Pass",
                        Body = body,
                        IsBodyHtml = true
                    };
                    msg.To.Add(request.VisitorEmail);
                    client.Send(msg);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Email dispatch error: {ex.Message}");
                throw new Exception($"Failed to dispatch visitor pass email: {ex.Message}", ex);
            }
        }

        private string GenerateHmacSignature(int requestId, long timestamp, string passCode)
        {
            string rawKey = $"{passCode}:{QrSecretSalt}";
            string rawData = $"{requestId}:{timestamp}";
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(rawKey)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                return Convert.ToBase64String(hash).Substring(0, 10);
            }
        }

        private static bool CryptographicEquals(string a, string b)
        {
            if (a == null || b == null) return a == b;
            byte[] aBytes = Encoding.UTF8.GetBytes(a);
            byte[] bBytes = Encoding.UTF8.GetBytes(b);
            int diff = aBytes.Length ^ bBytes.Length;
            for (int i = 0; i < aBytes.Length && i < bBytes.Length; i++)
                diff |= aBytes[i] ^ bBytes[i];
            return diff == 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose(); base.Dispose(disposing);
        }
    }
}