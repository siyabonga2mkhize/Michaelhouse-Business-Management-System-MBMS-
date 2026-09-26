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
    public class MyProfileController : BaseController
    {
        private DBContextClass db = new DBContextClass();
        private CafeteriaAuditService Audit { get { return new CafeteriaAuditService(db); } }

        private int CurrentStudentId
        {
            get
            {
                int id;
                if (Session["StudentId"] != null && int.TryParse(Session["StudentId"].ToString(), out id))
                    return id;
                return 0;
            }
        }

        private string CurrentRole
        {
            get { return Session["UserRole"] != null ? Session["UserRole"].ToString() : ""; }
        }

        private bool IsStudent()
        {
            return CurrentRole == "Student" && CurrentStudentId > 0;
        }

        // ══════════════════════════════════════════════════════════════
        // MY PROFILE (combined view)
        // ══════════════════════════════════════════════════════════════
        public ActionResult MyProfile()
        {
            if (!IsStudent())
            {
                TempData["Error"] = "Only students can access this page.";
                return RedirectToAction("Index", "Home");
            }

            int sid = CurrentStudentId;
            var student = db.Students.FirstOrDefault(s => s.StudentId == sid);
            if (student == null) return HttpNotFound();

            var profile = db.StudentProfiles.FirstOrDefault(p => p.StudentId == sid);

            var memberships = db.TeamMemberships
                .Include("Team.Sport")
                .Where(m => m.StudentId == sid && m.EndDate == null)
                .ToList();

            var dietary = db.StudentDietaryRecords
                .Include("Allergen")
                .Include("DietaryCategory")
                .Where(r => r.StudentId == sid
                         && r.IsActive
                         && r.Status == DietaryRequestStatus.Approved)
                .ToList();

            var pending = db.DietaryChangeRequests
                .Where(r => r.StudentId == sid && r.Status == DietaryRequestStatus.Pending)
                .OrderByDescending(r => r.SubmittedAt)
                .ToList();

            var assignment = db.ResidenceAssignments
                .Include("Residence")
                .Include("Room")
                .FirstOrDefault(a => a.StudentId == sid && a.IsActive);

            ViewBag.Student = student;
            ViewBag.Profile = profile;
            ViewBag.Memberships = memberships;
            ViewBag.DietaryRecords = dietary;
            ViewBag.PendingRequests = pending;
            ViewBag.Assignment = assignment;
            ViewBag.Role = CurrentRole;
            return View();
        }

        // ══════════════════════════════════════════════════════════════
        // DIETARY INFO (focused page)
        // ══════════════════════════════════════════════════════════════
        public ActionResult Dietary()
        {
            if (!IsStudent()) return RedirectToAction("Index", "Home");

            int sid = CurrentStudentId;

            var records = db.StudentDietaryRecords
                .Include("Allergen")
                .Include("DietaryCategory")
                .Where(r => r.StudentId == sid)
                .OrderByDescending(r => r.IsActive)
                .ThenByDescending(r => r.RequestedAt)
                .ToList();

            var pending = db.DietaryChangeRequests
                .Where(r => r.StudentId == sid && r.Status == DietaryRequestStatus.Pending)
                .OrderByDescending(r => r.SubmittedAt)
                .ToList();

            ViewBag.Student = db.Students.FirstOrDefault(s => s.StudentId == sid);
            ViewBag.PendingRequests = pending;
            ViewBag.Role = CurrentRole;
            return View(records);
        }

        // ══════════════════════════════════════════════════════════════
        // REQUEST CHANGE — form
        // ══════════════════════════════════════════════════════════════
        public ActionResult RequestChange()
        {
            if (!IsStudent()) return RedirectToAction("Index", "Home");

            ViewBag.Allergens = db.Allergens.Where(a => a.IsActive).OrderBy(a => a.Name).ToList();
            ViewBag.Categories = db.DietaryCategories.Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
            ViewBag.Student = db.Students.FirstOrDefault(s => s.StudentId == CurrentStudentId);
            ViewBag.Role = CurrentRole;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RequestChange(int recordType, int? allergenId, int? categoryId,
            string customDescription, string reason)
        {
            if (!IsStudent()) return RedirectToAction("Index", "Home");

            int sid = CurrentStudentId;

            if (recordType < 0 || recordType > 4)
            {
                TempData["Error"] = "Invalid record type.";
                return RedirectToAction("RequestChange");
            }

            var type = (DietaryRecordType)recordType;

            if (type == DietaryRecordType.Allergy && !allergenId.HasValue && string.IsNullOrWhiteSpace(customDescription))
            {
                TempData["Error"] = "Please specify which allergen or describe the allergy.";
                return RedirectToAction("RequestChange");
            }

            if (type == DietaryRecordType.DietaryPreference && !categoryId.HasValue && string.IsNullOrWhiteSpace(customDescription))
            {
                TempData["Error"] = "Please select a dietary category or describe your preference.";
                return RedirectToAction("RequestChange");
            }

            bool safetyReview = (type == DietaryRecordType.Allergy ||
                                 type == DietaryRecordType.MedicalRestriction);

            bool existingMatch = db.StudentDietaryRecords.Any(r =>
                r.StudentId == sid &&
                r.IsActive &&
                r.Status == DietaryRequestStatus.Approved &&
                r.RecordType == type &&
                ((allergenId.HasValue && r.AllergenId == allergenId) ||
                 (categoryId.HasValue && r.DietaryCategoryId == categoryId)));

            var request = new DietaryChangeRequest
            {
                StudentId = sid,
                RecordType = type,
                ChangeType = existingMatch ? DietaryChangeType.Update : DietaryChangeType.Add,
                RequestedAllergenId = allergenId,
                RequestedDietaryCategoryId = categoryId,
                RequestedDescription = customDescription,
                Reason = reason,
                Status = DietaryRequestStatus.Pending,
                SubmittedAt = DateTime.Now,
                SafetyReviewRequired = safetyReview
            };

            db.DietaryChangeRequests.Add(request);
            db.SaveChanges();

            Audit.LogDietaryChangeRequest(request.Id, sid, type.ToString());

            if (safetyReview)
                TempData["Success"] = "Request submitted. A Dietitian will review it. " +
                                       "Until then, your current approved restrictions remain active.";
            else
                TempData["Success"] = "Request submitted for review.";

            return RedirectToAction("Dietary");
        }

        // ══════════════════════════════════════════════════════════════
        // WITHDRAW A PENDING REQUEST
        // ══════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult WithdrawRequest(int id)
        {
            if (!IsStudent()) return RedirectToAction("Index", "Home");

            int sid = CurrentStudentId;
            var req = db.DietaryChangeRequests.FirstOrDefault(r => r.Id == id && r.StudentId == sid);
            if (req == null) return HttpNotFound();

            if (req.Status == DietaryRequestStatus.Pending)
            {
                req.Status = DietaryRequestStatus.Withdrawn;
                db.SaveChanges();

                Audit.Log("DietaryRequestWithdrawn", "DietaryChangeRequest", id, "Pending", "Withdrawn");
                TempData["Success"] = "Request withdrawn.";
            }

            return RedirectToAction("Dietary");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}