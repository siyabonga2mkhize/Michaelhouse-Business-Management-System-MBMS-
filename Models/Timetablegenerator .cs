using Microsoft.EntityFrameworkCore;
using Michaelhouse.Infrastructure;
﻿using System;
using System.Collections.Generic;
using System.Linq;
using Michaelhouse.Models;
using Michaelhouse.Models.Enums;

namespace Michaelhouse.Services
{
    public class TimetableGenerator
    {
        // ── School structure ──────────────────────────────────────────────────────
        private const int DAYS = 5;   // Mon-Fri
        private const int PERIODS_PER_DAY = 7;  // 7 teaching periods
        private const int ACADEMIC_YEAR = 2026;

        // Grades in the school
        private static readonly int[] Grades = { 8, 9, 10, 11, 12 };

        // How many times per week each subject should appear
        // Core subjects appear more often than stream subjects
        private static readonly Dictionary<string, int> SubjectFrequency =
            new Dictionary<string, int>
        {
            { "English Home Language",   5 }, // Every day
            { "Life Orientation",        2 },
            { "Mathematics",             5 },
            { "Mathematical Literacy",   4 },
            // All other subjects (stream, language): 3 times per week
        };

        private const int DEFAULT_FREQUENCY = 3;

        // ── Generate full timetable ───────────────────────────────────────────────

        /// <summary>
        /// Generates timetable for all grades for the given academic year.
        /// Clears existing timetable first.
        /// Returns (success, errors).
        /// </summary>
        public (bool Success, List<string> Errors) GenerateTimetable(int academicYear)
        {
            var errors = new List<string>();

            using (var db = DbContextFactory.Create())
            {
                // Clear existing timetable
                var existing = db.TimetableSlots
                    .Where(ts => ts.AcademicYear == academicYear).ToList();
                db.TimetableSlots.RemoveRange(existing);
                db.SaveChanges();

                // Load all teachers and their assignments
                var teachers = db.Teachers
                    .Include("SubjectAssignments")
                    .Include("SubjectAssignments.Subject")
                    .ToList();

                // Load periods
                var periods = db.Periods
                    .Where(p => !p.IsBreak)
                    .OrderBy(p => p.PeriodNumber)
                    .ToList();

                if (!periods.Any())
                {
                    errors.Add("No periods defined. Please create periods first.");
                    return (false, errors);
                }

                // Build the slot grid: [day][period] → list of occupied (teacherId, grade, stream)
                // This tracks what is already scheduled to avoid clashes
                var teacherBusy = new HashSet<string>(); // "teacherId_day_period"
                var gradeBusy = new HashSet<string>(); // "grade_stream_day_period"

                var slotsToAdd = new List<TimetableSlot>();

                // Process each grade
                foreach (var grade in Grades)
                {
                    var gradeErrors = ScheduleGrade(
                        grade, academicYear, teachers, periods,
                        teacherBusy, gradeBusy, slotsToAdd, db);

                    errors.AddRange(gradeErrors);
                }

                db.TimetableSlots.AddRange(slotsToAdd);
                db.SaveChanges();

                return (!errors.Any(), errors);
            }
        }

        // ── Schedule a single grade ───────────────────────────────────────────────

        private List<string> ScheduleGrade(
            int grade, int academicYear,
            List<Teacher> teachers, List<Period> periods,
            HashSet<string> teacherBusy, HashSet<string> gradeBusy,
            List<TimetableSlot> slotsToAdd,
            DBContextClass db)
        {
            var errors = new List<string>();

            // Build the list of "lessons to schedule" for this grade
            var lessons = BuildLessonList(grade, teachers, db);

            if (!lessons.Any())
            {
                errors.Add($"Grade {grade}: No teacher assignments found. Assign teachers before generating timetable.");
                return errors;
            }

            // Shuffle lessons to distribute them evenly
            var rng = new Random();
            lessons = lessons.OrderBy(_ => rng.Next()).ToList();

            foreach (var lesson in lessons)
            {
                int scheduled = 0;
                int needed = lesson.FrequencyPerWeek;

                // Try to place this lesson in available slots
                for (int day = 1; day <= DAYS && scheduled < needed; day++)
                {
                    foreach (var period in periods)
                    {
                        if (scheduled >= needed) break;

                        var teacherKey = $"{lesson.TeacherId}_{day}_{period.PeriodId}";
                        var gradeKey = $"{grade}_{(int)lesson.Stream}_{day}_{period.PeriodId}";

                        // Check for clashes
                        if (teacherBusy.Contains(teacherKey)) continue;
                        if (gradeBusy.Contains(gradeKey)) continue;

                        // For compulsory subjects (Stream=None), also check
                        // that no other stream lesson for this grade is at this time
                        if (lesson.Stream == AcademicStream.None)
                        {
                            bool gradeOccupied = false;
                            foreach (AcademicStream s in Enum.GetValues(typeof(AcademicStream)))
                            {
                                if (gradeBusy.Contains($"{grade}_{(int)s}_{day}_{period.PeriodId}"))
                                {
                                    gradeOccupied = true;
                                    break;
                                }
                            }
                            if (gradeOccupied) continue;
                        }

                        // ── Slot is free — schedule it ────────────────────────
                        teacherBusy.Add(teacherKey);
                        gradeBusy.Add(gradeKey);

                        slotsToAdd.Add(new TimetableSlot
                        {
                            AcademicYear = academicYear,
                            DayOfWeek = day,
                            PeriodId = period.PeriodId,
                            TeacherId = lesson.TeacherId,
                            SubjectId = lesson.SubjectId,
                            Grade = grade,
                            Stream = lesson.Stream
                        });

                        scheduled++;
                    }
                }

                if (scheduled < needed)
                {
                    errors.Add(
                        $"Grade {grade}: Could only schedule {scheduled}/{needed} periods " +
                        $"for {lesson.SubjectName}. Consider adjusting teacher assignments or periods.");
                }
            }

            return errors;
        }

        // ── Build lesson list for a grade ─────────────────────────────────────────

        private List<LessonToSchedule> BuildLessonList(
            int grade, List<Teacher> teachers, DBContextClass db)
        {
            var lessons = new List<LessonToSchedule>();

            var assignments = db.TeacherSubjectGrades
                .Include("Teacher")
                .Include("Subject")
                .Where(tsg => tsg.Grade == grade)
                .ToList();

            foreach (var assignment in assignments)
            {
                int freq = SubjectFrequency.ContainsKey(assignment.Subject.Name)
                    ? SubjectFrequency[assignment.Subject.Name]
                    : DEFAULT_FREQUENCY;

                lessons.Add(new LessonToSchedule
                {
                    TeacherId = assignment.TeacherId,
                    SubjectId = assignment.SubjectId,
                    SubjectName = assignment.Subject.Name,
                    Stream = assignment.Stream,
                    FrequencyPerWeek = freq
                });
            }

            return lessons;
        }

        // ── Helper class ──────────────────────────────────────────────────────────

        private class LessonToSchedule
        {
            public int TeacherId { get; set; }
            public int SubjectId { get; set; }
            public string SubjectName { get; set; }
            public AcademicStream Stream { get; set; }
            public int FrequencyPerWeek { get; set; }
        }

        // ── Seed default periods ──────────────────────────────────────────────────

        /// <summary>
        /// Creates the default Michaelhouse period structure.
        /// Call once on first setup.
        /// </summary>
        public void SeedPeriods()
        {
            using (var db = DbContextFactory.Create())
            {
                if (db.Periods.Any()) return;

                var periods = new[]
                {
                    new Period { PeriodNumber = 1, StartTime = new TimeSpan(7, 30, 0),  EndTime = new TimeSpan(8, 25, 0),  Label = "Period 1" },
                    new Period { PeriodNumber = 2, StartTime = new TimeSpan(8, 25, 0),  EndTime = new TimeSpan(9, 20, 0),  Label = "Period 2" },
                    new Period { PeriodNumber = 3, StartTime = new TimeSpan(9, 20, 0),  EndTime = new TimeSpan(10, 15, 0), Label = "Period 3" },
                    new Period { PeriodNumber = 0, StartTime = new TimeSpan(10, 15, 0), EndTime = new TimeSpan(10, 35, 0), Label = "Break",    IsBreak = true },
                    new Period { PeriodNumber = 4, StartTime = new TimeSpan(10, 35, 0), EndTime = new TimeSpan(11, 30, 0), Label = "Period 4" },
                    new Period { PeriodNumber = 5, StartTime = new TimeSpan(11, 30, 0), EndTime = new TimeSpan(12, 25, 0), Label = "Period 5" },
                    new Period { PeriodNumber = 0, StartTime = new TimeSpan(12, 25, 0), EndTime = new TimeSpan(13, 10, 0), Label = "Lunch",    IsBreak = true },
                    new Period { PeriodNumber = 6, StartTime = new TimeSpan(13, 10, 0), EndTime = new TimeSpan(14, 5, 0),  Label = "Period 6" },
                    new Period { PeriodNumber = 7, StartTime = new TimeSpan(14, 5, 0),  EndTime = new TimeSpan(15, 0, 0),  Label = "Period 7" },
                };

                db.Periods.AddRange(periods);
                db.SaveChanges();
            }
        }

        // ── Get timetable for a grade ─────────────────────────────────────────────

        public List<TimetableSlot> GetTimetableForGrade(int grade, int academicYear)
        {
            using (var db = DbContextFactory.Create())
            {
                return db.TimetableSlots
                    .Include("Period")
                    .Include("Teacher")
                    .Include("Subject")
                    .Where(ts => ts.Grade == grade && ts.AcademicYear == academicYear)
                    .OrderBy(ts => ts.DayOfWeek)
                    .ThenBy(ts => ts.Period.PeriodNumber)
                    .ToList();
            }
        }

        // ── Get timetable for a teacher ───────────────────────────────────────────

        public List<TimetableSlot> GetTimetableForTeacher(int teacherId, int academicYear)
        {
            using (var db = DbContextFactory.Create())
            {
                return db.TimetableSlots
                    .Include("Period")
                    .Include("Subject")
                    .Where(ts => ts.TeacherId == teacherId && ts.AcademicYear == academicYear)
                    .OrderBy(ts => ts.DayOfWeek)
                    .ThenBy(ts => ts.Period.PeriodNumber)
                    .ToList();
            }
        }

        // ── Get timetable for a student ───────────────────────────────────────────

        public List<TimetableSlot> GetTimetableForStudent(int studentId, int academicYear)
        {
            using (var db = DbContextFactory.Create())
            {
                // Get student's grade and stream
                var reg = db.Registrations
                    .FirstOrDefault(r => r.StudentId == studentId);
                if (reg == null) return new List<TimetableSlot>();

                var streamEnr = db.StreamEnrolments
                    .FirstOrDefault(se => se.StudentId == studentId);

                var grade = reg.GradeEnrolling;
                var stream = streamEnr?.Stream ?? AcademicStream.None;

                // Get all slots for this grade that the student should attend:
                // 1. Compulsory slots (Stream = None) — all students
                // 2. Stream-specific slots matching student's stream
                return db.TimetableSlots
                    .Include("Period")
                    .Include("Teacher")
                    .Include("Subject")
                    .Where(ts =>
                        ts.Grade == grade &&
                        ts.AcademicYear == academicYear &&
                        (ts.Stream == AcademicStream.None || ts.Stream == stream))
                    .OrderBy(ts => ts.DayOfWeek)
                    .ThenBy(ts => ts.Period.PeriodNumber)
                    .ToList();
            }
        }
    }
}