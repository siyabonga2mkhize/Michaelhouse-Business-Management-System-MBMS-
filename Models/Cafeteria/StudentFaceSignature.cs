using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC16 — One row per student, storing their enrolled face
    // signature. The photo itself is NEVER stored — only the
    // 128-dimension encoding produced by the recognition library.
    // ============================================================

    public class StudentFaceSignature
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Student")]
        [Index(IsUnique = true)]
        public int StudentId { get; set; }

        /// <summary>
        /// The 128-dimension face encoding, serialised as a comma-separated
        /// list of numbers. This is not reversible into a photo.
        /// </summary>
        [Required]
        public string FaceEncoding { get; set; }

        public DateTime? ConsentGivenAt { get; set; }

        [Required]
        public DateTime EnrolledAt { get; set; }

        public bool IsActive { get; set; }

        public virtual Student Student { get; set; }

        public StudentFaceSignature()
        {
            EnrolledAt = DateTime.UtcNow;
            IsActive = true;
        }
    }
}