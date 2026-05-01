using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Michaelhouse.Controllers
{
    public class DriverDocument
    {
        public int Id { get; set; }
        public int DriverApplicationId { get; set; }
        public string FilePath { get; set; }
        public string DocumentType { get; set; } // "ID", "Licence", "Other"
        public string OtherDocumentType { get; set; } // optional descriptor when DocumentType == "Other"

        public virtual DriverApplication DriverApplication { get; set; }
    }
}