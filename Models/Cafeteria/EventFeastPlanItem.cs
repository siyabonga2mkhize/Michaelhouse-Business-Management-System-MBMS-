using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC19 — One dish on a feast plan and how much to prepare.
    //
    // Guests in the dish's category are shared between that
    // category's dishes (one portion per guest); a Common dish
    // covers everyone. The buffer is added and rounded up:
    //   TotalQuantity = BaseQuantity + BufferQuantity
    // ============================================================
    public class EventFeastPlanItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("FeastPlan")]
        public int FeastPlanId { get; set; }
        public virtual EventFeastPlan FeastPlan { get; set; }

        // The meal from the Dietitian's meal library
        [ForeignKey("MenuItem")]
        public int? MenuItemId { get; set; }
        public virtual MenuItem MenuItem { get; set; }

        // Copied so the plan still reads correctly if the meal is renamed
        [Required]
        [StringLength(200)]
        public string DishName { get; set; }

        // "Main" / "Side" / "Dessert" … from the event's buffet
        [StringLength(50)]
        public string Section { get; set; }

        [Required]
        public FeastDishCategory Category { get; set; }

        // Guests this dish is prepared for (one portion each)
        public int GuestsCovered { get; set; }

        public int BaseQuantity { get; set; }
        public int BufferQuantity { get; set; }
        public int TotalQuantity { get; set; }

        public int SortOrder { get; set; }

        public EventFeastPlanItem()
        {
            Section = "Main";
        }
    }
}
