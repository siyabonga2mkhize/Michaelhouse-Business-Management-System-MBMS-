using System;

namespace Michaelhouse.Helpers
{
    public static class GeofencingService
    {
        // Add your Location class here or in Models
        public class Location
        {
            public double lat { get; set; }
            public double lng { get; set; }
            public Location(double lat, double lng) { this.lat = lat; this.lng = lng; }
        }

        public static double GetDistance(Location pos1, Location pos2)
        {
            double e = pos1.lat * (Math.PI / 180);
            double f = pos1.lng * (Math.PI / 180);
            double g = pos2.lat * (Math.PI / 180);
            double h = pos2.lng * (Math.PI / 180);
            double i = (Math.Cos(e) * Math.Cos(g) * Math.Cos(f) * Math.Cos(h)
                        + Math.Cos(e) * Math.Sin(f) * Math.Cos(g) * Math.Sin(h)
                        + Math.Sin(e) * Math.Sin(g));
            double j = Math.Acos(i);
            return (6371 * j); // Result in Kilometers
        }

        public static bool IsWithinRadius(Location center, Location current, double radiusInMeters)
        {
            // Convert radius meters to km for comparison
            return GetDistance(center, current) <= (radiusInMeters * 0.001);
        }
    }
}