using Michaelhouse.Models.Cafeteria;
using System.Collections.Generic;

namespace Michaelhouse.Models.ViewModels
{
    // ============================================================
    // UC19 — Generate Feast Plan: form input for step 7 (edit and
    // recalculate). Quantities are never typed in; they are always
    // recalculated from the RSVPs, the dishes and the buffer.
    // ============================================================

    public class FeastPlanEditInput
    {
        public FeastPlanEditInput()
        {
            Items = new List<FeastPlanEditLine>();
        }

        public int Id { get; set; }

        // EventFeastPlan.RowVersion, base64 — detects edits made
        // by someone else since the page was opened
        public string RowVersion { get; set; }

        public decimal BufferPercent { get; set; }

        public string CoordinatorNotes { get; set; }

        public List<FeastPlanEditLine> Items { get; set; }

        // Optional: a meal from the meal library to add to the plan
        public int? AddMenuItemId { get; set; }
        public FeastDishCategory AddCategory { get; set; }
        public string AddSection { get; set; }
    }

    public class FeastPlanEditLine
    {
        public int Id { get; set; }
        public FeastDishCategory Category { get; set; }
        public bool Remove { get; set; }
    }
}
