using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC17 — One line in a menu template.
    // Links to the existing MenuItem catalogue so UC19 can
    // reuse the same Recipe + Ingredient BOM engine as UC15.
    // ============================================================
    public class EventMenuTemplateItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Template")]
        public int TemplateId { get; set; }
        public virtual EventMenuTemplate Template { get; set; }

        [Required]
        [ForeignKey("MenuItem")]
        public int MenuItemId { get; set; }
        public virtual MenuItem MenuItem { get; set; }

        // How many portions of this dish to prepare PER GUEST.
        // 1.0 = one portion per guest. 1.2 = allows for seconds.
        public decimal QuantityPerGuest { get; set; }

        // "Main" / "Side" / "Dessert" / "Drinks" / "Starter"
        [StringLength(50)]
        public string Section { get; set; }

        public int SortOrder { get; set; }

        public EventMenuTemplateItem()
        {
            QuantityPerGuest = 1.0m;
            Section = "Main";
        }
    }
}