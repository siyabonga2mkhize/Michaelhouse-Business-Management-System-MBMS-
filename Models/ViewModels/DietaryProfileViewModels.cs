using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models.ViewModels
{
    // ============================================================
    // Student dietary profile — the form the student edits.
    //
    // Four separate kinds of information:
    //   1. Dietary preference   (one choice)
    //   2. Food allergies       (many, plus "Other")
    //   3. Medical dietary restrictions (many, plus "Other")
    //   4. Additional dietary notes
    // ============================================================

    public class DietaryProfileViewModel
    {
        public DietaryProfileViewModel()
        {
            DietaryPreference = "None";
            SelectedAllergies = new List<string>();
            SelectedMedicalRestrictions = new List<string>();
        }

        public string StudentName { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // ── 1. Preference ────────────────────────────────────────
        [Display(Name = "Dietary preference")]
        public string DietaryPreference { get; set; }

        [StringLength(200)]
        [Display(Name = "Describe your dietary preference")]
        public string DietaryPreferenceOther { get; set; }

        // ── 2. Allergies ─────────────────────────────────────────
        // Allergy codes, e.g. "Peanuts", "Dairy". "Other" means
        // OtherAllergies must be filled in.
        public List<string> SelectedAllergies { get; set; }

        // Comma-separated list of allergies not in the checklist
        [StringLength(300)]
        [Display(Name = "Other allergies")]
        public string OtherAllergies { get; set; }

        // ── 3. Medical dietary restrictions ──────────────────────
        public List<string> SelectedMedicalRestrictions { get; set; }

        [StringLength(200)]
        [Display(Name = "Describe the medical dietary restriction")]
        public string MedicalRestrictionOther { get; set; }

        // ── 4. Notes ─────────────────────────────────────────────
        [StringLength(1000)]
        [Display(Name = "Additional dietary information")]
        public string DietaryNotes { get; set; }
    }
}
