using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Michaelhouse.Models.Enums
{
    public enum ApplicationStatus
    {
        Pending = 0,
        UnderAiReview = 1,
        AwaitingAdminDecision = 2,
        Approved = 3,
        Rejected = 4,
        Flagged = 5
    }
}