using System;
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
        // 📝 1. CREATE VISITOR REQUEST FORM
        // ============================================================

        [HttpGet]
        public ActionResult Create()
        {
            var model = new VisitorAccessRequest
            {
                StudentId = 1,
                BoardingHouseName = "Founders House",
                VisitDate = DateTime.Today
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(VisitorAccessRequest model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // --- GUARDRAIL 1: Time Sequence Validation ---
            if (model.StartTime >= model.EndTime)
            {
                ModelState.AddModelError("", "Invalid Schedule: The Start Time must be before the End Time.");
                return View(model);
            }

            // --- MICHAELHOUSE RULES ENGINE ---

            // RULE 0: Visiting hours based on day of week
            TimeSpan weekdayStart = new TimeSpan(16, 0, 0);
            TimeSpan weekdayEnd = new TimeSpan(19, 0, 0);
            TimeSpan weekendStart = new TimeSpan(9, 0, 0);
            TimeSpan weekendEnd = new TimeSpan(18, 0, 0);

            bool isWeekend = (model.VisitDate.DayOfWeek == DayOfWeek.Saturday ||
                              model.VisitDate.DayOfWeek == DayOfWeek.Sunday);

            TimeSpan allowedStart = isWeekend ? weekendStart : weekdayStart;
            TimeSpan allowedEnd = isWeekend ? weekendEnd : weekdayEnd;

            if (model.StartTime < allowedStart || model.EndTime > allowedEnd)
            {
                string dayType = isWeekend ? "weekend (09:00 - 18:00)" : "weekday (16:00 - 19:00)";
                ModelState.AddModelError("", $"Visits are only permitted during {dayType}.");
                return View(model);
            }

            // RULE 1: Closed Weekend Restriction
            bool isClosedWeekend = db.TermCalendars.Any(c =>
                c.IsClosedWeekend &&
                model.VisitDate >= c.StartDate &&
                model.VisitDate <= c.EndDate);

            if (isClosedWeekend)
            {
                ModelState.AddModelError("", "The selected date falls on a Closed Weekend.");
                return View(model);
            }

            // RULE 2: Prep (18:30 - 20:30) & Curfew (21:00) Overlap Check
            TimeSpan prepStart = new TimeSpan(18, 30, 0);
            TimeSpan prepEnd = new TimeSpan(20, 30, 0);
            TimeSpan curfew = new TimeSpan(21, 0, 0);

            bool overlapsWithPrep = !(model.StartTime >= prepEnd || model.EndTime <= prepStart);
            if (overlapsWithPrep)
            {
                ModelState.AddModelError("", "Visit window overlaps with Evening Prep (18:30 - 20:30).");
                return View(model);
            }

            if (model.EndTime > curfew)
            {
                ModelState.AddModelError("", "The requested end time exceeds House Curfew (21:00).");
                return View(model);
            }

            // RULE 3: Safeguarding & Zone Enforcement
            model.FinalAssignedZone = model.RequestedZone;
            string zoneMessage = string.Empty;

            if (!model.IsParentOrGuardian && model.RequestedZone == VisitorAccessZone.HouseCommonRoom)
            {
                model.FinalAssignedZone = VisitorAccessZone.PublicCampusGrounds;
                zoneMessage = " (Reassigned to Public Campus Grounds for safeguarding compliance)";
            }

            // RULE 4: Smart Routing (Auto-approval vs. Housemaster Review)
            model.AccessGatePassCode = "MH-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            model.CreatedAt = DateTime.Now;

            if (model.IsParentOrGuardian)
            {
                model.Status = VisitorRequestStatus.AutoApprovedWeekend;
                db.VisitorAccessRequests.Add(model);
                db.SaveChanges();

                // ✅ Send the real email with a link
                SendVisitorPassEmail(model);

                TempData["Success"] = $"✅ Auto-Approved! Visitor Pass emailed to {model.VisitorEmail}. {zoneMessage}";
                return RedirectToAction("MyRequests");
            }
            else
            {
                model.Status = VisitorRequestStatus.PendingHousemaster;
                db.VisitorAccessRequests.Add(model);
                db.SaveChanges();

                TempData["Success"] = $"📨 Sent to Housemaster for review.{zoneMessage}";
                return RedirectToAction("MyRequests");
            }
        }

        // ============================================================
        // 📋 2. STUDENT DASHBOARD
        // ============================================================

        [HttpGet]
        public ActionResult MyRequests()
        {
            var requests = db.VisitorAccessRequests
                .Where(r => r.StudentId == 1)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();
            return View(requests);
        }

        // ============================================================
        // 🏫 3. HOUSEMASTER DASHBOARD
        // ============================================================

        [HttpGet]
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
        [ValidateAntiForgeryToken]
        public ActionResult ReviewRequest(int id, string action)
        {
            var request = db.VisitorAccessRequests.Find(id);
            if (request == null) return HttpNotFound();

            if (string.Equals(action, "Approve", StringComparison.OrdinalIgnoreCase))
            {
                request.Status = VisitorRequestStatus.Approved;
                db.SaveChanges();

                // ✅ Send the real email with a link
                SendVisitorPassEmail(request);

                TempData["StudentNotification"] = $"✅ Housemaster approved {request.VisitorFullName}'s visit!";
                TempData["Success"] = $"✅ Approved! Visitor Pass emailed to {request.VisitorEmail}.";
            }
            else if (string.Equals(action, "Reject", StringComparison.OrdinalIgnoreCase))
            {
                request.Status = VisitorRequestStatus.RejectedPolicyViolation;
                request.AccessGatePassCode = null;
                TempData["StudentNotification"] = $"❌ Housemaster rejected {request.VisitorFullName}'s visit.";
                TempData["Error"] = $"Rejected visit for {request.VisitorFullName}.";
            }

            request.ActionedByHousemasterId = 999;
            request.ActionedAt = DateTime.Now;
            request.HousemasterRemarks = "Actioned via Housemaster Queue Dashboard.";
            db.SaveChanges();

            return RedirectToAction("HousemasterQueue");
        }

        // ============================================================
        // 🎟️ 4. VISITOR PASS PAGE (Opened from email link)
        // ============================================================

        [HttpGet]
        public ActionResult VisitorPass(int id)
        {
            var request = db.VisitorAccessRequests.Find(id);
            if (request == null) return HttpNotFound();
            return View(request);
        }

        // ============================================================
        // 🛡️ 5. DYNAMIC QR CODE GENERATION & SECURITY VALIDATION
        // ============================================================

        [HttpGet]
        public ActionResult ScanGate() => View();

        public class QrPayload { public int rid { get; set; } public long ts { get; set; } public string sig { get; set; } }

        [HttpGet]
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
                return File(qrCode.GetGraphic(20), "image/png");
            }
        }

        [HttpGet]
        public ActionResult GetQrTimestamp(int requestId)
        {
            var request = db.VisitorAccessRequests.Find(requestId);
            if (request == null) return Json(new { success = false }, JsonRequestBehavior.AllowGet);
            return Json(new { success = true, timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult ValidateGatePass(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
                return Json(new { success = false, message = "No QR scanner payload provided." }, JsonRequestBehavior.AllowGet);

            try
            {
                var payload = JsonConvert.DeserializeObject<QrPayload>(data);
                if (payload == null || payload.rid <= 0 || string.IsNullOrEmpty(payload.sig))
                    return Json(new { success = false, message = "Invalid QR code payload structure." }, JsonRequestBehavior.AllowGet);

                var nowUtc = DateTime.UtcNow;
                var qrTime = DateTimeOffset.FromUnixTimeSeconds(payload.ts).UtcDateTime;
                var age = nowUtc - qrTime;

                if (age < TimeSpan.FromSeconds(-30) || age > TimeSpan.FromSeconds(240))
                    return Json(new { success = false, message = "QR code expired. Refresh screen for new token." }, JsonRequestBehavior.AllowGet);

                var request = db.VisitorAccessRequests.FirstOrDefault(r => r.RequestId == payload.rid);
                if (request == null || string.IsNullOrEmpty(request.AccessGatePassCode))
                    return Json(new { success = false, message = "Unrecognized Gate Pass." }, JsonRequestBehavior.AllowGet);

                string expectedSignature = GenerateHmacSignature(payload.rid, payload.ts, request.AccessGatePassCode);
                if (!CryptographicEquals(payload.sig, expectedSignature))
                    return Json(new { success = false, message = "Security Check Failed: Invalid signature detected." }, JsonRequestBehavior.AllowGet);

                if (request.Status != VisitorRequestStatus.Approved && request.Status != VisitorRequestStatus.AutoApprovedWeekend)
                    return Json(new { success = false, message = $"Access Denied: Status is {request.Status}." }, JsonRequestBehavior.AllowGet);

                var nowLocal = DateTime.Now;
                if (nowLocal.Date != request.VisitDate.Date)
                    return Json(new { success = false, message = $"Pass active for date: {request.VisitDate:dd MMM yyyy}." }, JsonRequestBehavior.AllowGet);

                var currentTime = nowLocal.TimeOfDay;
                if (currentTime < request.StartTime || currentTime > request.EndTime)
                {
                    string startFormatted = request.StartTime.ToString(@"hh\:mm");
                    string endFormatted = request.EndTime.ToString(@"hh\:mm");
                    return Json(new { success = false, message = $"Pass valid between {startFormatted} and {endFormatted}." }, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    success = true,
                    message = $"Access Granted. Welcome to Michaelhouse, {request.VisitorFullName}!",
                    visitorName = request.VisitorFullName,
                    assignedZone = request.FinalAssignedZone.ToString()
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Parsing error: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ============================================================
        // 📧 EMAIL SENDING HELPER (Send a link instead of an image)
        // ============================================================

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

                if (string.IsNullOrEmpty(smtpHost))
                {
                    System.Diagnostics.Debug.WriteLine("SMTP not configured. Email not sent.");
                    return;
                }

                // Build the secure link to the dynamic pass page
                string passLink = $"{Request.Url.Scheme}://{Request.Url.Authority}/VisitorAccess/VisitorPass/{request.RequestId}";

                string body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; color: #333;'>
                    <h2 style='color: #C21E2E;'>Michaelhouse Visitor Pass</h2>
                    <p>Dear {request.VisitorFullName},</p>
                    <p>Your visit to Michaelhouse has been approved.</p>
                    <hr />
                    <p><strong>Date:</strong> {request.VisitDate:dd MMM yyyy}</p>
                    <p><strong>Time:</strong> {request.StartTime} - {request.EndTime}</p>
                    <p><strong>Zone:</strong> {request.FinalAssignedZone}</p>
                    <hr />
                    <p><strong>To access your time-sensitive Gate Pass, click the button below:</strong></p>
                    <a href='{passLink}' style='display: inline-block; padding: 12px 24px; background-color: #C21E2E; color: #ffffff; text-decoration: none; border-radius: 5px; font-weight: bold;'>View Your Gate Pass</a>
                    <br /><br />
                    <p><small>Note: The QR code on this page is time-sensitive and automatically refreshes every 4 minutes for your security.</small></p>
                    <hr />
                    <p>Thank you,<br />Michaelhouse Security</p>
                </body>
                </html>";

                using (var client = new SmtpClient(smtpHost, smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(smtpUser, smtpPass);

                    var msg = new MailMessage
                    {
                        From = new MailAddress(fromEmail, fromName),
                        Subject = "Your Visitor Pass for Michaelhouse",
                        Body = body,
                        IsBodyHtml = true
                    };
                    msg.To.Add(request.VisitorEmail);
                    client.Send(msg);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Email send failed: {ex.Message}");
            }
        }

        // ============================================================
        // 🔒 HELPER METHODS
        // ============================================================

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
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}