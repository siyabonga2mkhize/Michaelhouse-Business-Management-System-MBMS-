using System;
using System.Collections.Generic;
using System.Linq;

namespace Michaelhouse.Models.Enums
{
    public enum RegistrationStatus
    {
        Pending = 0,          // Application approved, parent not started registration
        InProgress = 1,       // Parent started registration
        SubjectsSelected = 2, // Subjects chosen (Grade 10-12)
        Completed = 3,        // Parent submitted registration, student account created
        Withdrawn = 4
    }
}