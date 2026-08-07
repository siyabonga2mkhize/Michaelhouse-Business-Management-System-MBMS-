using Michaelhouse.Models.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Michaelhouse.Models
{
    public class Registration
    {
        [Key]
        public int RegistrationId { get; set; }

        [ForeignKey("Application")]
        public int AppId { get; set; }

        [ForeignKey("Student")]
        public int StudentId { get; set; }

        public int GradeEnrolling { get; set; }

        public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Set when parent completes registration
        public DateTime? CompletedAt { get; set; }

        [DataType(DataType.MultilineText)]
        public string Notes { get; set; }

        // Navigation
        public virtual Application Application { get; set; }
        public virtual Student Student { get; set; }
    }
}