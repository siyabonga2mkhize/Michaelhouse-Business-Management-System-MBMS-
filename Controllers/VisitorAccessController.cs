using System;
using System.Linq;
using System.Web.Mvc;
using Michaelhouse.Models;

namespace Michaelhouse.Controllers
{
    public class VisitorAccessController : Controller
    {
        private DBContextClass db = new DBContextClass();

        // --- 1. THE FORM (Student fills this out) ---
        [HttpGet]
        public ActionResult Create()
        {
            // Hardcoded StudentId = 1 for demo purposes
            var model = new VisitorAccessRequest
            {
                StudentId = 1,
                BoardingHouseName = "Founders House"
            };
            return View(model);
        }

        // --- 2. PROCESS THE FORM (Rules Engine runs here) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(VisitorAccessRequest model)
        {
            if (ModelState.IsValid)
            {
                // --- 🚨 NEW GUARDRAIL: Check if Start Time is before End Time ---
                if (model.StartTime >= model.EndTime)
                {
                    TempData["Error"] = "Invalid Schedule: The Start Time must be before the End Time.";
                    return RedirectToAction("Create");
                }

                // --- MICHAELHOUSE RULES ENGINE ---

                // RULE 1: Closed Weekend Restriction
                bool isClosedWeekend = false;
                if (db.TermCalendars.Any())
                {
                    isClosedWeekend = db.TermCalendars
                        .Any(c => c.IsClosedWeekend && model.VisitDate >= c.StartDate && model.VisitDate <= c.EndDate);
                }

                if (isClosedWeekend)
                {
                    model.Status = VisitorRequestStatus.RejectedClosedWeekend;
                    db.VisitorAccessRequests.Add(model);
                    db.SaveChanges();
                    TempData["Error"] = "Request blocked: This date is a Closed Weekend.";
                    return RedirectToAction("MyRequests");
                }

                // RULE 2: Prep (18:30 - 20:30) & Curfew (21:00)
                TimeSpan prepStart = TimeSpan.FromHours(18.5);
                TimeSpan prepEnd = TimeSpan.FromHours(20.5);
                TimeSpan curfew = TimeSpan.FromHours(21);

                if (model.StartTime >= prepStart && model.StartTime < prepEnd)
                {
                    model.Status = VisitorRequestStatus.RejectedPolicyViolation;
                    db.VisitorAccessRequests.Add(model);
                    db.SaveChanges();
                    TempData["Error"] = "Request blocked: Visitors cannot visit during Evening Prep.";
                    return RedirectToAction("MyRequests");
                }

                if (model.EndTime > curfew)
                {
                    model.Status = VisitorRequestStatus.RejectedPolicyViolation;
                    db.VisitorAccessRequests.Add(model);
                    db.SaveChanges();
                    TempData["Error"] = "Request blocked: The visit passes House Curfew.";
                    return RedirectToAction("MyRequests");
                }

                // RULE 3: Safeguarding & Zone Enforcement
                model.FinalAssignedZone = model.RequestedZone;
                string zoneMessage = "";

                if (!model.IsParentOrGuardian && model.RequestedZone == VisitorAccessZone.HouseCommonRoom)
                {
                    model.FinalAssignedZone = VisitorAccessZone.PublicCampusGrounds;
                    zoneMessage = " (Auto-redirected to Public Campus Grounds for safeguarding)";
                }

                // RULE 4: Smart Routing
                if (model.IsParentOrGuardian && !isClosedWeekend)
                {
                    model.Status = VisitorRequestStatus.AutoApprovedWeekend;
                    model.AccessGatePassCode = "MH-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                    TempData["Success"] = $"✅ Auto-Approved! Your Gate Pass is: {model.AccessGatePassCode} {zoneMessage}";
                }
                else
                {
                    model.Status = VisitorRequestStatus.PendingHousemaster;
                    model.AccessGatePassCode = "MH-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                    TempData["Success"] = $"📨 Request sent to Housemaster for review. {zoneMessage}";
                }

                db.VisitorAccessRequests.Add(model);
                db.SaveChanges();

                return RedirectToAction("MyRequests");
            }

            return View(model);
        }

        // --- 3. MY REQUESTS (Student sees their passes) ---
        [HttpGet]
        public ActionResult MyRequests()
        {
            // Get all requests for StudentId = 1
            var requests = db.VisitorAccessRequests
                .Where(r => r.StudentId == 1)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            return View(requests);
        }

        // ============================================================
        // 🏫 HOUSEMASTER DASHBOARD
        // ============================================================

        [HttpGet]
        public ActionResult HousemasterQueue()
        {
            // Get all requests that are currently pending
            var pendingRequests = db.VisitorAccessRequests
                .Where(r => r.Status == VisitorRequestStatus.PendingHousemaster)
                .OrderBy(r => r.VisitDate)
                .ToList();

            return View(pendingRequests);
        }

        [HttpPost]
        public ActionResult ReviewRequest(int id, string action)
        {
            var request = db.VisitorAccessRequests.Find(id);
            if (request == null) return HttpNotFound();

            if (action == "Approve")
            {
                request.Status = VisitorRequestStatus.Approved;

                // Simulating an Email/SMS notification by setting a special TempData flag
                TempData["StudentNotification"] = $"✅ Housemaster approved {request.VisitorFullName}'s visit!";
                TempData["Success"] = $"Approved visit for {request.VisitorFullName}.";
            }
            else if (action == "Reject")
            {
                request.Status = VisitorRequestStatus.RejectedPolicyViolation;
                request.AccessGatePassCode = null; // Invalidate the pass

                // Simulating an Email/SMS notification by setting a special TempData flag
                TempData["StudentNotification"] = $"❌ Housemaster rejected {request.VisitorFullName}'s visit.";
                TempData["Error"] = $"Rejected visit for {request.VisitorFullName}.";
            }

            // Log the review
            request.ActionedByHousemasterId = 999; // Hardcoded for demo
            request.ActionedAt = DateTime.Now;
            request.HousemasterRemarks = $"Actioned by Housemaster via queue dashboard.";

            db.SaveChanges();

            return RedirectToAction("HousemasterQueue");
        }
    }
}