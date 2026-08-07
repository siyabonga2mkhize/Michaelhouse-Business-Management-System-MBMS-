using Microsoft.AspNetCore.Authorization;
using Michaelhouse.Infrastructure;
﻿using Michaelhouse.Models;
using Michaelhouse.Services;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Michaelhouse.Controllers
{
    [Authorize(Roles = "Admin,HouseMaster")]
    public class StudentQRCodeController : BaseController
    {
        private DBContextClass db = DbContextFactory.Create();
        private StudentQRCodeService service = new StudentQRCodeService();

        // -----------------------------------------------
        // View / print card
        // -----------------------------------------------
        public ActionResult Details(int studentId)
        {
            var student = db.Students.Find(studentId);
            if (student == null)
                return NotFound();

            var qr = service.GetActiveQRCode(studentId)
                      ?? service.GenerateQRCode(studentId); // auto-generate if missing

            ViewBag.Student = student;
            return View(qr);
        }

        public ActionResult Print(int studentId)
        {
            var student = db.Students.Find(studentId);
            if (student == null)
                return NotFound();

            var qr = service.GetActiveQRCode(studentId)
                      ?? service.GenerateQRCode(studentId);

            ViewBag.Student = student;
            return View(qr);
        }

        // -----------------------------------------------
        // Download raw PNG
        // -----------------------------------------------
        public FileResult Download(int studentId)
        {
            var student = db.Students.Find(studentId);
            var qr = service.GetActiveQRCode(studentId);

            if (qr == null || qr.QRImage == null)
                return null;

            string fileName = $"{qr.QRCodeValue}.png";
            return File(qr.QRImage, "image/png", fileName);
        }

        // Inline version — used as the src of an <img> tag so the
        // QR renders directly on Details/Print without a separate download
        public FileResult Image(int studentId)
        {
            var qr = service.GetActiveQRCode(studentId);

            if (qr == null || qr.QRImage == null)
                return null;

            return File(qr.QRImage, "image/png");
        }

        // -----------------------------------------------
        // Regenerate — Admin only
        // -----------------------------------------------
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Regenerate(int studentId)
        {
            service.RegenerateQRCode(studentId);

            TempData["Success"] = "QR code regenerated successfully.";
            return RedirectToAction("Details", new { studentId });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}