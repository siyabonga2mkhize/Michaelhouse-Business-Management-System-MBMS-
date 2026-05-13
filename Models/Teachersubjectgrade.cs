using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Michaelhouse.Models.Enums;

namespace Michaelhouse.Models
{
    /// <summary>
    /// A teacher is assigned to teach a specific Subject in a specific Grade.
    /// Rules enforced:
    ///   - A subject in a grade can only be assigned to ONE teacher.
    ///   - A teacher can have a maximum of TWO assignments.
    /// </summary>
    public class TeacherSubjectGrade
    {
        [Key]
        public int TeacherSubjectGradeId { get; set; }

        [ForeignKey("Teacher")]
        public int TeacherId { get; set; }

        [ForeignKey("Subject")]
        public int SubjectId { get; set; }

        // Grade this assignment applies to (8-12)
        [Required]
        public int Grade { get; set; }

        // For Grade 10-12 stream subjects — which stream does this apply to?
        // None = applies to all (compulsory subjects like English, LO)
        public AcademicStream Stream { get; set; } = AcademicStream.None;

        // Navigation
        public virtual Teacher Teacher { get; set; }
        public virtual Subject Subject { get; set; }
    }
}