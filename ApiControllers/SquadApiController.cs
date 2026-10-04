using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.ApiControllers
{
    // ============================================================
    // UC12 Mobile — Coach Squad Status
    //
    //   GET  /api/squad                    → list sports + students
    //   POST /api/squad/mark               → toggle available/unavailable
    // ============================================================
    
    [RoutePrefix("api/squad")]
    public class SquadApiController : Controller
    {
        private readonly DBContextClass _db;

        public SquadApiController()
        {
            _db = new DBContextClass();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        // ============================================================
        // GET /api/squad
        // ============================================================

        [HttpGet]
        [Route("")]
        public JsonResult Index()
        {
            try
            {
                var all = _db.StudentSportStatuses
                    .Include("Student")
                    .OrderBy(x => x.Sport)
                    .ThenBy(x => x.Student.LastName)
                    .ToList();

                var sports = all
                    .GroupBy(x => x.Sport)
                    .OrderBy(g => g.Key)
                    .Select(g => new
                    {
                        sportName = g.Key,
                        totalCount = g.Count(),
                        activeCount = g.Count(x => x.IsActive),
                        students = g.Select(s => new
                        {
                            statusId = s.Id,
                            studentId = s.StudentId,
                            studentName = s.Student != null
                                ? s.Student.FirstName + " " + s.Student.LastName
                                : "Unknown",
                            studentNumber = s.Student != null ? s.Student.StudentNumber : "",
                            isActive = s.IsActive,
                            statusReason = s.StatusReason,
                            unavailableUntil = s.UnavailableUntil.HasValue
                                ? s.UnavailableUntil.Value.ToString("yyyy-MM-dd")
                                : null
                        }).ToList()
                    })
                    .ToList();

                return Json(new { ok = true, sports = sports },
                    JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message },
                    JsonRequestBehavior.AllowGet);
            }
        }

        // ============================================================
        // POST /api/squad/mark
        // Body: { statusId, isActive, reason, unavailableUntil }
        // ============================================================

        [HttpPost]
        [Route("mark")]
        public JsonResult Mark()
        {
            try
            {
                string body;
                using (var reader = new StreamReader(Request.InputStream))
                {
                    body = reader.ReadToEnd();
                }

                var data = JsonConvert.DeserializeObject<MarkModel>(body);
                if (data == null || data.StatusId <= 0)
                {
                    return Json(new { ok = false, error = "Missing statusId." });
                }

                var entry = _db.StudentSportStatuses.FirstOrDefault(x => x.Id == data.StatusId);
                if (entry == null)
                {
                    return Json(new { ok = false, error = "Student sport record not found." });
                }

                entry.IsActive = data.IsActive;
                entry.StatusReason = string.IsNullOrWhiteSpace(data.Reason)
                    ? (data.IsActive ? null : "Unavailable")
                    : data.Reason.Trim();

                if (data.IsActive)
                {
                    entry.StatusReason = null;
                    entry.UnavailableUntil = null;
                }
                else if (!string.IsNullOrWhiteSpace(data.UnavailableUntil))
                {
                    DateTime d;
                    if (DateTime.TryParse(data.UnavailableUntil, out d))
                    {
                        entry.UnavailableUntil = d.Date;
                    }
                }

                entry.UpdatedAt = DateTime.UtcNow;
                _db.SaveChanges();

                return Json(new
                {
                    ok = true,
                    statusId = entry.Id,
                    isActive = entry.IsActive
                });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = "Server error: " + ex.Message });
            }
        }
    }

    public class MarkModel
    {
        public int StatusId { get; set; }
        public bool IsActive { get; set; }
        public string Reason { get; set; }
        public string UnavailableUntil { get; set; }
    }
}