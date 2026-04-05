using System.Collections.Generic;
using System.Web.Mvc;

namespace Michaelhouse.Models
{
    public class EnrollmentViewModel
    {
        public int SelectedStudentId { get; set; }
        public int SelectedSubjectId { get; set; }
        public string AcademicYear { get; set; }

        // These lists populate the Dropdown menus
        public IEnumerable<SelectListItem> StudentList { get; set; }
        public IEnumerable<SelectListItem> SubjectList { get; set; }
    }
}