using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    public class Coach
    {
        [Key]
        public int CoachID { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; }

        [Required, MaxLength(100)]
        public string Surname { get; set; }

        [ForeignKey("Sport")]
        public int? SportId { get; set; }
        public virtual Sport Sport { get; set; }

        [MaxLength(50)]
        public string SportName { get; set; }

        [MaxLength(50)]
        public string Team { get; set; }

        [MaxLength(200)]
        [EmailAddress]
        public string Email { get; set; }

        [MaxLength(20)]
        public string Phone { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public virtual ICollection<Team> Teams { get; set; } = new List<Team>();
    }
}