using System;
using System.Collections.Generic;
using System.Linq;

namespace Michaelhouse.Models.Enums
{
    /// <summary>
    /// Academic streams available for Grade 10-12.
    /// Students are grouped by stream per grade.
    /// </summary>
    public enum AcademicStream
    {
        None = 0,                    // Grade 8 & 9 — no stream
        ArtsAndCulture = 1,          // Music, Visual Arts, Dramatic Arts
        HumanAndSocialStudies = 2,   // Geography, History, Life Orientation
        Sciences = 3,                // Physical Sciences, Life Sciences, Further Studies
        Commerce = 4,                // Accounting, Economics
        EngineeringAndTechnology = 5 // Engineering Graphics, CAT, IT, Coding & Robotics
    }
}