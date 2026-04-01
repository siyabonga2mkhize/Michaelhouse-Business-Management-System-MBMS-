using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace MIEMS.Models
{
	public class MaintananceRequest
	{
        public int MaintenanceRequestId { get; set; }

        [Required, Display(Name = "Title")]
        public string Title { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; }

        [Display(Name = "Location")]
        public string Location { get; set; }

        [Display(Name = "Priority")]
        public string Priority { get; set; } // Low, Medium, High, Critical

        [Display(Name = "Status")]
        public string Status { get; set; } // Open, In Progress, Completed

        [Display(Name = "Reported Date")]
        [DataType(DataType.Date)]
        public DateTime ReportedDate { get; set; }

        [Display(Name = "Completed Date")]
        [DataType(DataType.Date)]
        public DateTime? CompletedDate { get; set; }

        [Display(Name = "Assigned To")]
        public string AssignedTo { get; set; }

        [Display(Name = "Reported By")]
        public string ReportedBy { get; set; }

        [Display(Name = "Category")]
        public string Category { get; set; } // Electrical, Plumbing, Structural, Grounds
    }
}