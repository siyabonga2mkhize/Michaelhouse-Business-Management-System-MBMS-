using System;

namespace Michaelhouse.Helpers
{
    public static class GeofencingService
    {
        public class Location
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }

            public Location(double lat, double lng)
            {
                Latitude = lat;
                Longitude = lng;
            }
        }

        public static double CalculateDistance(Location a, Location b)
        {
            var R = 6371000; // Earth radius in meters
            var dLat = (b.Latitude - a.Latitude) * Math.PI / 180;
            var dLon = (b.Longitude - a.Longitude) * Math.PI / 180;
            var lat1 = a.Latitude * Math.PI / 180;
            var lat2 = b.Latitude * Math.PI / 180;
            var aVal = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1) * Math.Cos(lat2) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(aVal), Math.Sqrt(1 - aVal));
            return R * c;
        }

        public static bool IsWithinRadius(Location school, Location teacher, double radiusMeters)
        {
            return CalculateDistance(school, teacher) <= radiusMeters;
        }
    }
}