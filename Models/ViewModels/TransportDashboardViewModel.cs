namespace Michaelhouse.Models.ViewModels
{
    /// <summary>
    /// View model for the Transport Manager dashboard.
    /// </summary>
    public class TransportDashboardViewModel
    {
        public int TotalApplications { get; set; }
        public int Pending { get; set; }
        public int InterviewScheduled { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }
    }
}
