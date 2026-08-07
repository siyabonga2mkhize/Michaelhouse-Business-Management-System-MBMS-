using Michaelhouse.Infrastructure;
﻿using Michaelhouse.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Michaelhouse.Controllers
{
    public class SchoolClassesController : BaseController
    {
        private DBContextClass db = DbContextFactory.Create();

        // GET: SchoolClasses/Index
        public async Task<ActionResult> Index()
        {
            // Include Students so we can access the collection count in the view
            var classes = await db.SchoolClasses.Include(c => c.Students).ToListAsync();
            return View(classes);
        }

        // GET: SchoolClasses/Create
        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(SchoolClass schoolClass)
        {
            if (ModelState.IsValid)
            {
                db.SchoolClasses.Add(schoolClass);
                await db.SaveChangesAsync();
                return RedirectToAction("Index");
            }
            return View(schoolClass);
        }
        public ActionResult SeedClasses()
        {
            // The SQL Command
            string sql = "INSERT INTO SchoolClasses (ClassName, GradeLevel) VALUES (@p0, @p1)";

            // Executing the command directly against the database
            db.Database.ExecuteSqlRaw(sql, "Grade 8A", 8);
            db.Database.ExecuteSqlRaw(sql, "Grade 9A", 9);
            db.Database.ExecuteSqlRaw(sql, "Grade 10A", 10);
            db.Database.ExecuteSqlRaw(sql, "Grade 10B", 10);
            db.Database.ExecuteSqlRaw(sql, "Grade 11A", 11);
            db.Database.ExecuteSqlRaw(sql, "Grade 11B", 11);
            db.Database.ExecuteSqlRaw(sql, "Grade 12B", 12);
            db.Database.ExecuteSqlRaw(sql, "Grade 12C", 12);

            return Content("Classes inserted successfully using SQL!");
        }
        // GET: SchoolClasses/ClassList/5
        public async Task<ActionResult> ClassList(int? id)
        {
            if (id == null) return BadRequest();

            var schoolClass = await db.SchoolClasses.FindAsync(id);
            if (schoolClass == null) return NotFound();

            // Get only students assigned to THIS class
            var students = await db.Students
                .Where(s => s.ClassId == id)
                .OrderBy(s => s.LastName)
                .ToListAsync();

            ViewBag.ClassName = schoolClass.ClassName;
            return View(students);
        }

        // GET: SchoolClasses/Edit/8
        public async Task<ActionResult> Edit(int? id)
        {
            if (id == null) return BadRequest();

            SchoolClass schoolClass = await db.SchoolClasses.FindAsync(id);
            if (schoolClass == null) return NotFound();

            return View(schoolClass);
        }

        // POST: SchoolClasses/Edit/8
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind("ClassId,ClassName,GradeLevel")] SchoolClass schoolClass)
        {
            if (ModelState.IsValid)
            {
                db.Entry(schoolClass).State = EntityState.Modified;
                await db.SaveChangesAsync();
                return RedirectToAction("Index");
            }
            return View(schoolClass);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }


    }
}