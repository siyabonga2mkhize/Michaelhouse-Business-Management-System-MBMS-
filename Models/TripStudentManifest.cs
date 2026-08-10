namespace Michaelhouse.Models
{
    public class TripStudentManifest
    {
        public int Id { get; set; }

        public int TripId { get; set; }
        public int VehicleId { get; set; }
        public int StudentId { get; set; }

        //public virtual Trip Trip { get; set; }
        public virtual Vehicle Vehicle { get; set; }
        public virtual Student Student { get; set; }
    }
}