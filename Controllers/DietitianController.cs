using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Models.Enums;
using Michaelhouse.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [RequireLogin]
    public class DietitianController : BaseController
    {
        private DBContextClass db = new DBContextClass();
        private CafeteriaAuditService Audit { get { return new CafeteriaAuditService(db); } }

        private int CurrentUserId
        {
            get
            {
                int id;
                if (Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out id))
                    return id;
                return 0;
            }
        }

        private string CurrentUserName
        {
            get { return Session["UserName"] != null ? Session["UserName"].ToString() : "Dietitian"; }
        }

        private string CurrentRole
        {
            get { return Session["UserRole"] != null ? Session["UserRole"].ToString() : ""; }
        }

        // ══════════════════════════════════════════════════════════════
        // DASHBOARD
        // ══════════════════════════════════════════════════════════════
        public ActionResult Index()
        {
            var weekAgo = DateTime.Now.AddDays(-7);

            ViewBag.PendingRequests = db.DietaryChangeRequests.Count(r => r.Status == DietaryRequestStatus.Pending);
            ViewBag.UnderReviewCount = db.DietaryChangeRequests.Count(r => r.Status == DietaryRequestStatus.UnderReview);
            ViewBag.ApprovedThisWeek = db.StudentDietaryRecords.Count(r =>
                r.Status == DietaryRequestStatus.Approved &&
                r.ApprovedAt.HasValue && r.ApprovedAt.Value >= weekAgo);
            ViewBag.TotalActiveRules = db.StudentDietaryRecords.Count(r => r.IsActive && r.Status == DietaryRequestStatus.Approved);
            ViewBag.Allergies = db.StudentDietaryRecords.Count(r =>
                r.RecordType == DietaryRecordType.Allergy &&
                r.IsActive && r.Status == DietaryRequestStatus.Approved);
            ViewBag.MedicalRestrict = db.StudentDietaryRecords.Count(r =>
                r.RecordType == DietaryRecordType.MedicalRestriction &&
                r.IsActive && r.Status == DietaryRequestStatus.Approved);
            ViewBag.Vegetarians = db.StudentDietaryRecords.Count(r =>
                r.RecordType == DietaryRecordType.DietaryPreference &&
                r.IsActive && r.Status == DietaryRequestStatus.Approved);

            ViewBag.RecentApproved = db.StudentDietaryRecords
                .Include("Student")
                .Include("Allergen")
                .Where(r => r.Status == DietaryRequestStatus.Approved && r.ApprovedAt.HasValue)
                .OrderByDescending(r => r.ApprovedAt)
                .Take(5).ToList();

            ViewBag.PendingList = db.DietaryChangeRequests
                .Include("Student")
                .Where(r => r.Status == DietaryRequestStatus.Pending)
                .OrderByDescending(r => r.SubmittedAt)
                .Take(5).ToList();

            ViewBag.Role = CurrentRole;
            return View();
        }

        // ══════════════════════════════════════════════════════════════
        // PENDING CHANGE REQUESTS
        // ══════════════════════════════════════════════════════════════
        public ActionResult PendingRequests(string status = "Pending")
        {
            DietaryRequestStatus target;
            if (!Enum.TryParse(status, true, out target))
                target = DietaryRequestStatus.Pending;

            var requests = db.DietaryChangeRequests
                .Include("Student")
                .Where(r => r.Status == target)
                .OrderByDescending(r => r.SafetyReviewRequired)
                .ThenByDescending(r => r.SubmittedAt)
                .ToList();

            ViewBag.StatusFilter = status;
            ViewBag.Role = CurrentRole;
            return View(requests);
        }

        // ══════════════════════════════════════════════════════════════
        // REVIEW A SINGLE REQUEST
        // ══════════════════════════════════════════════════════════════
        public ActionResult ReviewRequest(int id)
        {
            var request = db.DietaryChangeRequests
                .Include("Student")
                .FirstOrDefault(r => r.Id == id);

            if (request == null) return HttpNotFound();

            // Existing approved records for this student (context for the review)
            var existing = db.StudentDietaryRecords
                .Include("Allergen")
                .Include("DietaryCategory")
                .Where(r => r.StudentId == request.StudentId
                         && r.IsActive
                         && r.Status == DietaryRequestStatus.Approved)
                .ToList();

            ViewBag.Allergen = request.RequestedAllergenId.HasValue
                ? db.Allergens.FirstOrDefault(a => a.Id == request.RequestedAllergenId.Value)
                : null;

            ViewBag.Category = request.RequestedDietaryCategoryId.HasValue
                ? db.DietaryCategories.FirstOrDefault(c => c.Id == request.RequestedDietaryCategoryId.Value)
                : null;

            ViewBag.ExistingRecords = existing;
            ViewBag.Role = CurrentRole;
            return View(request);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ApproveRequest(int id, string reviewNotes)
        {
            var request = db.DietaryChangeRequests.Find(id);
            if (request == null) return HttpNotFound();

            // Create the actual dietary record
            var record = new StudentDietaryRecord
            {
                StudentId = request.StudentId,
                RecordType = request.RecordType,
                AllergenId = request.RequestedAllergenId,
                DietaryCategoryId = request.RequestedDietaryCategoryId,
                CustomDescription = request.RequestedDescription,
                Status = DietaryRequestStatus.Approved,
                IsActive = true,
                RequestedByStudent = true,
                RequestedAt = request.SubmittedAt,
                ApprovedByUserId = CurrentUserId,
                ApprovedAt = DateTime.Now,
                ReviewNotes = reviewNotes ?? "",
                EffectiveFrom = DateTime.Now
            };
            db.StudentDietaryRecords.Add(record);

            request.Status = DietaryRequestStatus.Approved;
            request.ReviewedByUserId = CurrentUserId;
            request.ReviewedAt = DateTime.Now;
            request.ReviewNotes = reviewNotes ?? "";
            db.SaveChanges();

            Audit.LogDietaryApproved(record.Id, request.StudentId,
                request.RecordType.ToString(),
                request.RequestedDescription ?? "record");

            TempData["Success"] = "Dietary change approved. Future meal planning will respect this restriction.";
            return RedirectToAction("PendingRequests");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectRequest(int id, string reviewNotes)
        {
            var request = db.DietaryChangeRequests.Find(id);
            if (request == null) return HttpNotFound();

            request.Status = DietaryRequestStatus.Rejected;
            request.ReviewedByUserId = CurrentUserId;
            request.ReviewedAt = DateTime.Now;
            request.ReviewNotes = reviewNotes ?? "";
            db.SaveChanges();

            Audit.Log("DietaryRejected", "DietaryChangeRequest", id,
                "Pending", "Rejected", reviewNotes);

            TempData["Success"] = "Dietary change request rejected.";
            return RedirectToAction("PendingRequests");
        }

        // ══════════════════════════════════════════════════════════════
        // STUDENTS WITH DIETARY RECORDS
        // ══════════════════════════════════════════════════════════════
        public ActionResult Students()
        {
            var studentIds = db.StudentDietaryRecords
                .Where(r => r.IsActive)
                .Select(r => r.StudentId)
                .Distinct()
                .ToList();

            var students = db.Students
                .Where(s => studentIds.Contains(s.StudentId) && s.IsActive)
                .OrderBy(s => s.LastName)
                .ToList();

            var summaries = new Dictionary<int, int>();
            foreach (var s in students)
            {
                summaries[s.StudentId] = db.StudentDietaryRecords
                    .Count(r => r.StudentId == s.StudentId
                             && r.IsActive
                             && r.Status == DietaryRequestStatus.Approved);
            }

            ViewBag.Summaries = summaries;
            ViewBag.Role = CurrentRole;
            return View(students);
        }

        // ══════════════════════════════════════════════════════════════
        // A SINGLE STUDENT'S DIETARY PROFILE
        // ══════════════════════════════════════════════════════════════
        public ActionResult StudentDietary(int id)
        {
            var student = db.Students.FirstOrDefault(s => s.StudentId == id);
            if (student == null) return HttpNotFound();

            var records = db.StudentDietaryRecords
                .Include("Allergen")
                .Include("DietaryCategory")
                .Where(r => r.StudentId == id)
                .OrderByDescending(r => r.IsActive)
                .ThenByDescending(r => r.RequestedAt)
                .ToList();

            var pendingRequests = db.DietaryChangeRequests
                .Where(r => r.StudentId == id && r.Status == DietaryRequestStatus.Pending)
                .OrderByDescending(r => r.SubmittedAt)
                .ToList();

            ViewBag.Student = student;
            ViewBag.PendingRequests = pendingRequests;
            ViewBag.Allergens = db.Allergens.Where(a => a.IsActive).OrderBy(a => a.Name).ToList();
            ViewBag.Categories = db.DietaryCategories.Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
            ViewBag.Role = CurrentRole;
            return View(records);
        }

        // ══════════════════════════════════════════════════════════════
        // DIRECT ADD (Dietitian override)
        // ══════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddRecordDirect(int studentId, int recordType, int? allergenId, int? categoryId, string customDescription, string notes)
        {
            var student = db.Students.FirstOrDefault(s => s.StudentId == studentId);
            if (student == null) return HttpNotFound();

            var record = new StudentDietaryRecord
            {
                StudentId = studentId,
                RecordType = (DietaryRecordType)recordType,
                AllergenId = allergenId,
                DietaryCategoryId = categoryId,
                CustomDescription = customDescription,
                Status = DietaryRequestStatus.Approved,
                IsActive = true,
                RequestedByStudent = false,
                RequestedAt = DateTime.Now,
                ApprovedByUserId = CurrentUserId,
                ApprovedAt = DateTime.Now,
                ReviewNotes = notes ?? "Added directly by Dietitian",
                EffectiveFrom = DateTime.Now
            };
            db.StudentDietaryRecords.Add(record);
            db.SaveChanges();

            Audit.LogDietaryApproved(record.Id, studentId, record.RecordType.ToString(),
                customDescription ?? (allergenId.HasValue ? "allergen #" + allergenId : "category #" + categoryId));

            TempData["Success"] = "Dietary record added.";
            return RedirectToAction("StudentDietary", new { id = studentId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeactivateRecord(int id)
        {
            var record = db.StudentDietaryRecords.Find(id);
            if (record == null) return HttpNotFound();

            record.IsActive = false;
            record.EffectiveTo = DateTime.Now;
            db.SaveChanges();

            Audit.Log("DietaryDeactivated", "StudentDietaryRecord", id,
                "Active", "Inactive", "Deactivated by Dietitian");

            TempData["Success"] = "Dietary record deactivated.";
            return RedirectToAction("StudentDietary", new { id = record.StudentId });
        }

        // ══════════════════════════════════════════════════════════════
        // CATALOGUES — Allergens
        // ══════════════════════════════════════════════════════════════
        public ActionResult Allergens()
        {
            var allergens = db.Allergens.OrderBy(a => a.Name).ToList();
            ViewBag.Role = CurrentRole;
            return View(allergens);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddAllergen(string name, string description)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Allergen name is required.";
                return RedirectToAction("Allergens");
            }

            if (!db.Allergens.Any(a => a.Name == name))
            {
                db.Allergens.Add(new Allergen
                {
                    Name = name.Trim(),
                    Description = description,
                    IsActive = true
                });
                db.SaveChanges();
                Audit.Log("AllergenAdded", "Allergen", 0, null, name);
            }
            TempData["Success"] = "Allergen added.";
            return RedirectToAction("Allergens");
        }

        // ══════════════════════════════════════════════════════════════
        // CATALOGUES — Dietary Categories
        // ══════════════════════════════════════════════════════════════
        public ActionResult DietaryCategories()
        {
            var cats = db.DietaryCategories.OrderBy(c => c.Name).ToList();
            ViewBag.Role = CurrentRole;
            return View(cats);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddDietaryCategory(string name, string description)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Category name is required.";
                return RedirectToAction("DietaryCategories");
            }

            if (!db.DietaryCategories.Any(c => c.Name == name))
            {
                db.DietaryCategories.Add(new DietaryCategory
                {
                    Name = name.Trim(),
                    Description = description,
                    IsActive = true
                });
                db.SaveChanges();
                Audit.Log("DietaryCategoryAdded", "DietaryCategory", 0, null, name);
            }
            TempData["Success"] = "Dietary category added.";
            return RedirectToAction("DietaryCategories");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}