using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models
{
    public class Parent
    {
        public int ParentId { get; set; }

        [Required, Display(Name = "First Name")]
        [StringLength(50)]
        public string FirstName { get; set; }

        [Required, Display(Name = "Last Name")]
        [StringLength(50)]
        public string LastName { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Required, Phone]
        [Display(Name = "Phone Number")]
        public string Phone { get; set; }

        [Display(Name = "Address")]
        public string Address { get; set; }

        [Display(Name = "Occupation")]
        public string Occupation { get; set; }

        [Display(Name = "Relationship")]
        public string Relationship { get; set; } // Father, Mother, Guardian

        public string FullName => FirstName + " " + LastName;

        public virtual ICollection<Student> Students { get; set; }
        public virtual ICollection<Application> Applications { get; set; }
    }
}
