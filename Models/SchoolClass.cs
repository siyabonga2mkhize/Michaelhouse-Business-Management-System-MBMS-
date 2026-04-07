using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models
{
    public class SchoolClass
    {
        [Key]
        public int ClassId { get; set; }
        public string ClassName { get; set; } // e.g., "12A"
        public int GradeLevel { get; set; }   // e.g., 12
        public virtual ICollection<Student> Students { get; set; }
        public virtual Teacher Teacher { get; set; }
    }
}