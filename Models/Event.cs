using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace MIEMS.Models
{
	public class Event
	{
        public int EventId { get; set; }

        [Required, Display(Name = "Event Name")]
        public string EventName { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; }

        [Display(Name = "Venue")]
        public string Venue { get; set; }

        [Display(Name = "Start Date")]
        [DataType(DataType.DateTime)]
        public DateTime StartDate { get; set; }

        [Display(Name = "End Date")]
        [DataType(DataType.DateTime)]
        public DateTime EndDate { get; set; }

        [Display(Name = "Capacity")]
        public int Capacity { get; set; }

        [Display(Name = "Ticket Price")]
        [DataType(DataType.Currency)]
        public decimal? TicketPrice { get; set; }

        [Display(Name = "Event Type")]
        public string EventType { get; set; } // Sports, Academic, Cultural, External

        [Display(Name = "Status")]
        public string Status { get; set; } // Scheduled, Ongoing, Completed, Cancelled
    }
}