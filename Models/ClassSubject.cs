using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models
{
    public class ClassSubject
    {
        [Key]
        public int Id { get; set; }
        public int ClassId { get; set; }
        public int SubjectId { get; set; }

        // Navigation properties are key for the dropdowns and lists!
        public virtual Subject Subject { get; set; }

        public string TeacherName { get; set; } //who is the teacher 
    }
}