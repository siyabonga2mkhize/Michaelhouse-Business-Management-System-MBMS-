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
    public class CoachController : BaseController
    {
        private DBContextClass db = new DBContextClass();
        private CafeteriaAuditService Audit { get { return new CafeteriaAuditService(db); } }

        private int CurrentCoachId
        {
            get
            {
                int id;
                if (Session["CoachId"] != null && int.TryParse(Session["CoachId"].ToString(), out id))
                    return id;
                return 0;
            }
        }

        private string CurrentUserName
        {
            get { return Session["UserName"] != null ? Session["UserName"].ToString() : "Coach"; }
        }

        private string CurrentRole
        {
            get { return Session["UserRole"] != null ? Session["UserRole"].ToString() : ""; }
        }

        /// <summary>
        /// Resolves the CoachId — checks session, then falls back to matching by email.
        /// </summary>
        private int ResolveCoachId()
        {
            int coachId = CurrentCoachId;
            if (coachId > 0) return coachId;

            int userId = 0;
            if (Session["UserId"] != null) int.TryParse(Session["UserId"].ToString(), out userId);
            if (userId == 0) return 0;

            var user = db.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null) return 0;

            var coach = db.Coaches.FirstOrDefault(c => c.Email == user.Email);
            if (coach != null)
            {
                Session["CoachId"] = coach.CoachID;
                return coach.CoachID;
            }
            return 0;
        }

        // ══════════════════════════════════════════════════════════════
        // DASHBOARD / MY TEAMS
        // ══════════════════════════════════════════════════════════════
        public ActionResult Index() { return RedirectToAction("MyTeam"); }

        public ActionResult MyTeam()
        {
            int coachId = ResolveCoachId();
            if (coachId == 0)
            {
                TempData["Error"] = "You are not assigned as a coach in the system.";
                return RedirectToAction("Index", "Home");
            }

            var myTeams = db.Teams
                .Include("Sport")
                .Where(t => t.CoachID == coachId && t.IsActive)
                .OrderBy(t => t.Name).ToList();

            var teamStats = new Dictionary<int, int>();
            foreach (var t in myTeams)
            {
                teamStats[t.TeamID] = db.TeamMemberships
                    .Count(m => m.TeamId == t.TeamID && m.EndDate == null);
            }

            ViewBag.TeamStats = teamStats;
            ViewBag.Coach = db.Coaches.FirstOrDefault(c => c.CoachID == coachId);
            ViewBag.Role = CurrentRole;
            return View(myTeams);
        }

        // ══════════════════════════════════════════════════════════════
        // TEAM DETAILS
        // ══════════════════════════════════════════════════════════════
        public ActionResult TeamDetails(int id)
        {
            int coachId = ResolveCoachId();
            if (coachId == 0) return RedirectToAction("Index", "Home");

            var team = db.Teams.Include("Sport").Include("Coach")
                .FirstOrDefault(t => t.TeamID == id && t.CoachID == coachId);
            if (team == null) return HttpNotFound();

            var memberships = db.TeamMemberships
                .Include("Student")
                .Where(m => m.TeamId == id)
                .OrderByDescending(m => m.EndDate == null)
                .ThenBy(m => m.Student.LastName).ToList();

            var activities = db.SportsActivities
                .Where(a => a.TeamId == id && a.StartDateTime >= DateTime.Now && !a.IsCancelled)
                .OrderBy(a => a.StartDateTime).Take(10).ToList();

            ViewBag.Team = team;
            ViewBag.Activities = activities;
            ViewBag.Role = CurrentRole;
            return View(memberships);
        }

        // ══════════════════════════════════════════════════════════════
        // CREATE ACTIVITY (Training / Match / etc.)
        // ══════════════════════════════════════════════════════════════
        public ActionResult CreateActivity(int id)
        {
            int coachId = ResolveCoachId();
            if (coachId == 0) return RedirectToAction("Index", "Home");

            var team = db.Teams.FirstOrDefault(t => t.TeamID == id && t.CoachID == coachId);
            if (team == null) return HttpNotFound();

            ViewBag.Team = team;
            ViewBag.Role = CurrentRole;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateActivity(int id, SportsActivity model, string startTime, string endTime)
        {
            int coachId = ResolveCoachId();
            if (coachId == 0) return RedirectToAction("Index", "Home");

            var team = db.Teams.FirstOrDefault(t => t.TeamID == id && t.CoachID == coachId);
            if (team == null) return HttpNotFound();

            if (string.IsNullOrWhiteSpace(model.Title))
            {
                TempData["Error"] = "Title is required.";
                return RedirectToAction("CreateActivity", new { id });
            }

            DateTime st, et;
            DateTime.TryParse(startTime, out st);
            DateTime.TryParse(endTime, out et);
            if (st == default) st = DateTime.Now.Date.AddHours(16);
            if (et == default || et < st) et = st.AddHours(2);

            var activity = new SportsActivity
            {
                TeamId = id,
                CoachId = coachId,
                ActivityType = model.ActivityType,
                Title = model.Title,
                StartDateTime = st,
                EndDateTime = et,
                Location = model.Location,
                Opponent = model.Opponent,
                IsAway = model.IsAway,
                Notes = model.Notes,
                MealRequirement = model.MealRequirement,
                SnackRequirement = model.SnackRequirement,
                HydrationRequirement = model.HydrationRequirement,
                SpecialTimingNote = model.SpecialTimingNote,
                IsLateRequirement = false,
                IsCancelled = false,
                CreatedAt = DateTime.Now,
                CreatedByUserId = Session["UserId"] != null
                    ? (int?)Convert.ToInt32(Session["UserId"]) : null
            };

            if (st < DateTime.Now.AddDays(7) && st > DateTime.Now)
                activity.IsLateRequirement = true;

            db.SportsActivities.Add(activity);
            db.SaveChanges();

            Audit.Log("CoachActivityCreated", "SportsActivity", activity.Id,
                null, activity.Title + " on " + st.ToString("ddd dd MMM HH:mm"));

            TempData["Success"] = "Activity created.";
            return RedirectToAction("TeamDetails", new { id });
        }

        // ══════════════════════════════════════════════════════════════
        // SUBMIT REQUIREMENTS
        // ══════════════════════════════════════════════════════════════
        public ActionResult SubmitRequirements(int? id)
        {
            int coachId = ResolveCoachId();
            if (coachId == 0) return RedirectToAction("Index", "Home");

            var myTeams = db.Teams
                .Where(t => t.CoachID == coachId && t.IsActive)
                .OrderBy(t => t.Name).ToList();

            if (!myTeams.Any())
            {
                TempData["Error"] = "You have no teams assigned.";
                return RedirectToAction("MyTeam");
            }

            int selectedTeamId = id ?? myTeams.First().TeamID;
            var selectedTeam = myTeams.FirstOrDefault(t => t.TeamID == selectedTeamId) ?? myTeams.First();

            ViewBag.MyTeams = myTeams;
            ViewBag.SelectedTeam = selectedTeam;
            ViewBag.MealSlots = db.MealSlots.Where(m => m.IsActive).OrderBy(m => m.DisplayOrder).ToList();
            ViewBag.Role = CurrentRole;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitRequirements(int id, string requirementText, string mealDateTime,
            int? mealSlotId, int playerCount, int serviceType)
        {
            int coachId = ResolveCoachId();
            if (coachId == 0) return RedirectToAction("Index", "Home");

            var team = db.Teams.FirstOrDefault(t => t.TeamID == id && t.CoachID == coachId);
            if (team == null) return HttpNotFound();

            if (string.IsNullOrWhiteSpace(requirementText))
            {
                TempData["Error"] = "Requirement text is required.";
                return RedirectToAction("SubmitRequirements", new { id });
            }

            var today = DateTime.Today;
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Sunday)) % 7;
            var weekStart = today.AddDays(-diff).Date;

            DateTime? mealDt = null;
            if (!string.IsNullOrWhiteSpace(mealDateTime))
            {
                DateTime parsed;
                if (DateTime.TryParse(mealDateTime, out parsed)) mealDt = parsed;
            }

            var req = new CoachRequirement
            {
                TeamId = id,
                CoachId = coachId,
                WeekStartDate = weekStart,
                MealDateTime = mealDt,
                MealSlotId = mealSlotId,
                ServiceType = (MealServiceType)serviceType,
                PlayerCount = playerCount <= 0 ? 0 : playerCount,
                RequirementText = requirementText,
                Status = CoachRequirementStatus.Submitted,
                IsLate = false,
                SubmittedAt = DateTime.Now
            };

            db.CoachRequirements.Add(req);
            db.SaveChanges();

            Audit.LogCoachRequirement(req.Id, id, "CoachRequirementSubmitted");

            TempData["Success"] = "Requirement submitted. Cafeteria will review.";
            return RedirectToAction("MyHistory");
        }

        // ══════════════════════════════════════════════════════════════
        // TEAM MESSAGES
        // ══════════════════════════════════════════════════════════════
        public ActionResult TeamMessages(int id)
        {
            int coachId = ResolveCoachId();
            if (coachId == 0) return RedirectToAction("Index", "Home");

            var team = db.Teams.FirstOrDefault(t => t.TeamID == id && t.CoachID == coachId);
            if (team == null) return HttpNotFound();

            var messages = db.CoachMessages
                .Where(m => m.TeamId == id)
                .OrderByDescending(m => m.SentAt).ToList();

            ViewBag.Team = team;
            ViewBag.Role = CurrentRole;
            return View(messages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SendTeamMessage(int id, string subject, string body, bool isUrgent = false)
        {
            int coachId = ResolveCoachId();
            if (coachId == 0) return RedirectToAction("Index", "Home");

            var team = db.Teams.FirstOrDefault(t => t.TeamID == id && t.CoachID == coachId);
            if (team == null) return HttpNotFound();

            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
            {
                TempData["Error"] = "Subject and message body are required.";
                return RedirectToAction("TeamMessages", new { id });
            }

            var msg = new CoachMessage
            {
                TeamId = id,
                CoachId = coachId,
                Subject = subject,
                Body = body,
                IsUrgent = isUrgent,
                SentAt = DateTime.Now
            };
            db.CoachMessages.Add(msg);
            db.SaveChanges();

            Audit.Log("TeamMessageSent", "CoachMessage", msg.Id, null, subject);

            // Notify all active members
            try
            {
                var userIds = db.TeamMemberships
                    .Where(m => m.TeamId == id && m.EndDate == null && m.Student.UserId != null)
                    .Select(m => m.Student.UserId.Value)
                    .Distinct().ToList();

                foreach (var uid in userIds)
                {
                    db.Notifications.Add(new Notification
                    {
                        UserId = uid,
                        Message = (isUrgent ? "🚨 " : "📣 ") + team.Name + ": " + subject,
                        RelatedEntityType = "CoachMessage",
                        RelatedEntityId = msg.Id,
                        CreatedAt = DateTime.Now,
                        IsRead = false
                    });
                }
                db.SaveChanges();
            }
            catch { }

            TempData["Success"] = "Message sent to team.";
            return RedirectToAction("TeamMessages", new { id });
        }

        // ══════════════════════════════════════════════════════════════
        // MY HISTORY
        // ══════════════════════════════════════════════════════════════
        public ActionResult MyHistory()
        {
            int coachId = ResolveCoachId();
            if (coachId == 0) return RedirectToAction("Index", "Home");

            var reqs = db.CoachRequirements
                .Include("Team")
                .Include("MealSlot")
                .Where(r => r.CoachId == coachId)
                .OrderByDescending(r => r.SubmittedAt).ToList();

            ViewBag.Role = CurrentRole;
            return View(reqs);
        }

        // ══════════════════════════════════════════════════════════════
        // CALENDAR
        // ══════════════════════════════════════════════════════════════
        public ActionResult Calendar()
        {
            int coachId = ResolveCoachId();
            if (coachId == 0) return RedirectToAction("Index", "Home");

            var teamIds = db.Teams
                .Where(t => t.CoachID == coachId && t.IsActive)
                .Select(t => t.TeamID).ToList();

            var upcoming = db.SportsActivities
                .Include("Team")
                .Where(a => teamIds.Contains(a.TeamId)
                         && a.StartDateTime >= DateTime.Now.AddDays(-7)
                         && a.StartDateTime <= DateTime.Now.AddDays(30)
                         && !a.IsCancelled)
                .OrderBy(a => a.StartDateTime).ToList();

            ViewBag.Teams = db.Teams.Where(t => teamIds.Contains(t.TeamID)).ToList();
            ViewBag.Role = CurrentRole;
            return View(upcoming);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}