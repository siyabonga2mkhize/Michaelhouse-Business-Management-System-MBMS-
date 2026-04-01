using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace Michaelhouse.Models
{
    public class Document
    {
        [Key]
        public int DId { get; set; }

        [ForeignKey("Student")]
        public int StudentId { get; set; }

        [ForeignKey("Application")]
        public int AppId { get; set; }

        [Required, MaxLength(100)]
        [Display(Name = "Document Type")]
        public string Type { get; set; } // e.g. "BirthCertificate", "ReportCard"

        [Required]
        public string FilePath { get; set; } // Stored file path or blob URL

        public string FileName { get; set; } // Original filename shown to users
        public string ContentType { get; set; } // MIME type e.g. application/pdf
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Student Student { get; set; }
        public Application Application { get; set; }
    }
}