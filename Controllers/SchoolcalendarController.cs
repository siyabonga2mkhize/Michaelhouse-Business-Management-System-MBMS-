using Michaelhouse.Filters;
using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using static Michaelhouse.Models.Schoolcalendarevent;

namespace Michaelhouse.Controllers
{
    /// <summary>
    /// Admin management of the school calendar (exams, test weeks, sports,
    /// holidays, etc.). The Events() JSON feed is deliberately NOT
    /// admin-only - students, parents and house masters all need to read it
    /// to render the calendar widget on their own pages.
    /// </summary>
    public class SchoolCalendarController : Controller
    {
        private readonly DBContextClass db = new DBContextClass();

        // GET: SchoolCalendar
        [AdminOnly]
        public ActionResult Index()
        {
            var events = db.SchoolCalendarEvents
                .Where(e => !e.IsArchived)
                .OrderBy(e => e.StartDate)
                .ToList();
            return View(events);
        }

        // GET: SchoolCalendar/Create
        [AdminOnly]
        public ActionResult Create()
        {
            return View(new SchoolCalendarEvent
            {
                StartDate = DateTime.Today,
                EndDate = DateTime.Today,
                Category = CalendarEventCategory.Other
            });
        }

        // POST: SchoolCalendar/Create
        [AdminOnly]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Title,Description,StartDate,EndDate,Category,DiscourageLeave")] SchoolCalendarEvent model)
        {
            if (model.EndDate.Date < model.StartDate.Date)
                ModelState.AddModelError("EndDate", "End date cannot be before the start date.");

            if (!ModelState.IsValid) return View(model);

            model.CreatedBy = Session["UserName"] as string ?? "Admin";
            model.CreatedAt = DateTime.Now;

            db.SchoolCalendarEvents.Add(model);
            db.SaveChanges();

            TempData["Success"] = "Calendar event created.";
            return RedirectToAction("Index");
        }

        // GET: SchoolCalendar/Edit/5
        [AdminOnly]
        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var evt = db.SchoolCalendarEvents.Find(id);
            if (evt == null) return HttpNotFound();
            return View(evt);
        }

        // POST: SchoolCalendar/Edit/5
        [AdminOnly]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "SchoolCalendarEventId,Title,Description,StartDate,EndDate,Category,DiscourageLeave")] SchoolCalendarEvent model)
        {
            var evt = db.SchoolCalendarEvents.Find(model.SchoolCalendarEventId);
            if (evt == null) return HttpNotFound();

            if (model.EndDate.Date < model.StartDate.Date)
                ModelState.AddModelError("EndDate", "End date cannot be before the start date.");

            if (!ModelState.IsValid) return View(model);

            evt.Title = model.Title;
            evt.Description = model.Description;
            evt.StartDate = model.StartDate;
            evt.EndDate = model.EndDate;
            evt.Category = model.Category;
            evt.DiscourageLeave = model.DiscourageLeave;

            db.SaveChanges();
            TempData["Success"] = "Calendar event updated.";
            return RedirectToAction("Index");
        }

        // POST: SchoolCalendar/Archive/5
        [AdminOnly]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Archive(int id)
        {
            var evt = db.SchoolCalendarEvents.Find(id);
            if (evt == null) return HttpNotFound();
            evt.IsArchived = true;
            db.SaveChanges();
            TempData["Success"] = "Calendar event removed.";
            return RedirectToAction("Index");
        }

        // GET: SchoolCalendar/Events?start=2026-08-01&end=2026-09-01
        // JSON feed in FullCalendar's expected shape. Any logged-in user
        // (student/parent/house master/admin) can read this - it's the same
        // calendar for everyone, just rendered read-only outside of admin.
        [RequireLogin]
        public JsonResult Events(DateTime? start, DateTime? end)
        {
            var query = db.SchoolCalendarEvents.Where(e => !e.IsArchived);

            if (start.HasValue) query = query.Where(e => e.EndDate >= start.Value.Date);
            if (end.HasValue) query = query.Where(e => e.StartDate <= end.Value.Date);

            var events = query
                .OrderBy(e => e.StartDate)
                .ToList()
                .Select(e => new
                {
                    id = e.SchoolCalendarEventId,
                    title = e.Title,
                    start = e.StartDate.ToString("yyyy-MM-dd"),
                    // FullCalendar treats "end" as exclusive for all-day events,
                    // so a same-day event needs end = start + 1 day to render.
                    end = e.EndDate.AddDays(1).ToString("yyyy-MM-dd"),
                    allDay = true,
                    color = CalendarEventCategory.ColorHex(e.Category),
                    extendedProps = new
                    {
                        category = CalendarEventCategory.DisplayName(e.Category),
                        discourageLeave = e.DiscourageLeave,
                        description = e.Description
                    }
                })
                .ToList();

            return Json(events, JsonRequestBehavior.AllowGet);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}