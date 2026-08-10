using System.Collections.Generic;

namespace Michaelhouse.Models
{
    public class RoomCompatibilityResult
    {
        public int RoomId { get; set; }
        public int ResidenceId { get; set; }
        public int BedId { get; set; }
        public string ResidenceName { get; set; }
        public string RoomNumber { get; set; }
        public string BedNumber { get; set; }
        public double CompatibilityScore { get; set; }
        public double Confidence { get; set; }
        public List<string> Reasons { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<OccupantCompatibilityBreakdown> OccupantBreakdown { get; set; } = new List<OccupantCompatibilityBreakdown>();
        public List<string> ScoreDetails { get; set; } = new List<string>();
    }

    public class OccupantCompatibilityBreakdown
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public double CompatibilityScore { get; set; }
        public List<string> Reasons { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
