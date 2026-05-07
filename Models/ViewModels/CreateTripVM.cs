using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;


    namespace Michaelhouse.Models.ViewModels
    {
        public class CreateTripVM
        {
            public string Destination { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }

            // Selected grade levels (e.g. 8,9,10)
            public List<int> SelectedGradeLevels { get; set; }

            // Individual students
            public List<int> SelectedStudentIds { get; set; }

            // UI data
            public List<Student> AllStudents { get; set; }
            public List<int> AvailableGradeLevels { get; set; }
        }
    }
