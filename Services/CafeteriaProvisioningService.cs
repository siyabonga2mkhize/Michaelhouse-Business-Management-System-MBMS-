using System;
using System.Collections.Generic;
using System.Linq;
using Michaelhouse.Models;
using Michaelhouse.Models.Enums;

namespace Michaelhouse.Services
{
    public class CafeteriaProvisioningService
    {
        private readonly DBContextClass _db;

        public CafeteriaProvisioningService() : this(new DBContextClass()) { }
        public CafeteriaProvisioningService(DBContextClass db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public void ProvisionFromWizard(int studentId)
        {
            var student = _db.Students.FirstOrDefault(s => s.StudentId == studentId);
            var profile = _db.StudentProfiles.FirstOrDefault(p => p.StudentId == studentId);
            if (student == null || profile == null) return;

            // Load lookup tables into MEMORY first
            var allergensInMemory = _db.Allergens.Where(a => a.IsActive).ToList();
            var categoriesInMemory = _db.DietaryCategories.Where(c => c.IsActive).ToList();
            var sportsInMemory = _db.Sports.Where(s => s.IsActive).ToList();

            // ─── 1. Allergies ─────────────────────────────────────
            try
            {
                foreach (var token in SplitCsv(profile.Allergies))
                {
                    var match = allergensInMemory.FirstOrDefault(a =>
                        a.Name.Equals(token, StringComparison.OrdinalIgnoreCase) ||
                        a.Name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        token.IndexOf(a.Name, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (match != null)
                        UpsertRecord(studentId, DietaryRecordType.Allergy, match.Id, null, null);
                    else
                        UpsertRecord(studentId, DietaryRecordType.Allergy, null, null, token);
                }
                _db.SaveChanges();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Allergy provisioning failed: " + ex.Message); }

            // ─── 2. Medical Conditions ────────────────────────────
            try
            {
                foreach (var token in SplitCsv(profile.MedicalConditions))
                    UpsertRecord(studentId, DietaryRecordType.MedicalRestriction, null, null, token);
                _db.SaveChanges();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Medical provisioning failed: " + ex.Message); }

            // ─── 3. Dietary Preferences ───────────────────────────
            try
            {
                foreach (var token in SplitCsv(profile.DietaryPreferences))
                {
                    var match = categoriesInMemory.FirstOrDefault(c =>
                        c.Name.Equals(token, StringComparison.OrdinalIgnoreCase) ||
                        c.Name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        token.IndexOf(c.Name, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (match != null)
                        UpsertRecord(studentId, DietaryRecordType.DietaryPreference, null, match.Id, null);
                    else
                        UpsertRecord(studentId, DietaryRecordType.DietaryPreference, null, null, token);
                }
                _db.SaveChanges();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Preference provisioning failed: " + ex.Message); }

            // ─── 4. Special Dietary Needs ─────────────────────────
            try
            {
                foreach (var token in SplitCsv(profile.SpecialDietaryNeeds))
                    UpsertRecord(studentId, DietaryRecordType.MedicalRestriction, null, null, token);
                _db.SaveChanges();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Special dietary provisioning failed: " + ex.Message); }

            // ─── 5. Sports (IN-MEMORY match, safe) ────────────────
            try
            {
                foreach (var token in SplitCsv(profile.Sports))
                {
                    if (token.Equals("None", StringComparison.OrdinalIgnoreCase)) continue;

                    var sport = sportsInMemory.FirstOrDefault(s =>
                        s.Name.Equals(token, StringComparison.OrdinalIgnoreCase) ||
                        s.Name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (sport == null) continue;

                    var team = _db.Teams.FirstOrDefault(t => t.SportId == sport.SportId && t.IsActive);
                    if (team == null) continue;

                    bool exists = _db.TeamMemberships.Any(m =>
                        m.StudentId == studentId && m.TeamId == team.TeamID && m.EndDate == null);
                    if (exists) continue;

                    _db.TeamMemberships.Add(new TeamMembership
                    {
                        StudentId = studentId,
                        TeamId = team.TeamID,
                        StartDate = DateTime.Now,
                        Status = TeamMembershipStatus.Pending,
                        RequestedByStudent = false,
                        Notes = "Provisioned from admission wizard",
                        CreatedDate = DateTime.Now
                    });
                }
                _db.SaveChanges();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Sports provisioning failed: " + ex.Message); }
        }

        public void ProvisionForNewStudent(int studentId)
        {
            try
            {
                bool anyDietary = _db.StudentDietaryRecords.Any(r => r.StudentId == studentId);
                bool anySport = _db.TeamMemberships.Any(m => m.StudentId == studentId);
                if (anyDietary || anySport) return;

                ProvisionFromWizard(studentId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ProvisionForNewStudent failed: " + ex.Message);
            }
        }

        // ─── helpers ─────────────────────────────────────────────────
        private void UpsertRecord(int studentId, DietaryRecordType type,
            int? allergenId, int? categoryId, string custom)
        {
            bool exists = _db.StudentDietaryRecords.Any(r =>
                r.StudentId == studentId &&
                r.RecordType == type &&
                r.IsActive &&
                r.Status == DietaryRequestStatus.Approved &&
                ((allergenId.HasValue && r.AllergenId == allergenId) ||
                 (categoryId.HasValue && r.DietaryCategoryId == categoryId) ||
                 (!string.IsNullOrEmpty(custom) && r.CustomDescription == custom)));
            if (exists) return;

            _db.StudentDietaryRecords.Add(new StudentDietaryRecord
            {
                StudentId = studentId,
                RecordType = type,
                AllergenId = allergenId,
                DietaryCategoryId = categoryId,
                CustomDescription = custom,
                Status = DietaryRequestStatus.Approved,
                IsActive = true,
                RequestedByStudent = false,
                RequestedAt = DateTime.Now,
                ApprovedAt = DateTime.Now,
                EffectiveFrom = DateTime.Now,
                ReviewNotes = "Provisioned automatically from admission wizard"
            });
        }

        private static IEnumerable<string> SplitCsv(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;
            foreach (var part in text.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = part.Trim();
                if (!string.IsNullOrWhiteSpace(t)) yield return t;
            }
        }
    }
}