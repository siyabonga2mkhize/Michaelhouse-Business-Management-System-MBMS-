using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models
{
    public class Sport
    {
        [Key]
        public int SportId { get; set; }

        [Required, StringLength(80)]
        public string Name { get; set; }

        [StringLength(30)]
        public string Season { get; set; }   // Winter / Summer / Year-round

        [StringLength(250)]
        public string Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<Team> Teams { get; set; } = new List<Team>();
    }
}