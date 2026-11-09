using System;
using System.Globalization;

namespace CampusSafeTwin.Classes
{
    public static class Geo
    {
        private const double EarthRadiusMetres = 6371000.0;

        // turns string coordinate into double number...
        public static double ParseCoordinate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Coordinate string is empty or null.", nameof(value));

            // changes commas to dots in case locale used comma,,,
            string normalized = value.Trim().Replace(',', '.');
            return double.Parse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        // tries parsing cordinates without throwing err...
        public static bool TryParseCoordinate(string value, out double result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string normalized = value.Trim().Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        // formats cordinates with six decimals...
        public static string FormatCoordinate(double coord)
        {
            return coord.ToString("F6", CultureInfo.InvariantCulture);
        }

        // calculates distance between two points in meters,,,
        public static double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            double dLat = ToRadians(lat2 - lat1);
            double dLon = ToRadians(lon2 - lon1);

            double rLat1 = ToRadians(lat1);
            double rLat2 = ToRadians(lat2);

            double a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                       Math.Cos(rLat1) * Math.Cos(rLat2) *
                       Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

            double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
            return EarthRadiusMetres * c;
        }

        // gets shortest distance from point to a line segment...
        public static double DistanceToSegment(double pLat, double pLon, double aLat, double aLon, double bLat, double bLon)
        {
            // projects coords to local meters for quick math,,,
            double refLatRad = ToRadians(-26.6890);
            double cosRef = Math.Cos(refLatRad);

            double ax = ToRadians(aLon) * EarthRadiusMetres * cosRef;
            double ay = ToRadians(aLat) * EarthRadiusMetres;

            double bx = ToRadians(bLon) * EarthRadiusMetres * cosRef;
            double by = ToRadians(bLat) * EarthRadiusMetres;

            double px = ToRadians(pLon) * EarthRadiusMetres * cosRef;
            double py = ToRadians(pLat) * EarthRadiusMetres;

            double dx = bx - ax;
            double dy = by - ay;
            double segLenSq = dx * dx + dy * dy;

            if (segLenSq < 1e-6)
            {
                // both points are basically same spot...
                double dpx = px - ax;
                double dpy = py - ay;
                return Math.Sqrt(dpx * dpx + dpy * dpy);
            }

            // finds nearest point on segment,,,
            double t = ((px - ax) * dx + (py - ay) * dy) / segLenSq;
            if (t < 0.0) t = 0.0;
            else if (t > 1.0) t = 1.0;

            double closeX = ax + t * dx;
            double closeY = ay + t * dy;

            double diffX = px - closeX;
            double diffY = py - closeY;
            return Math.Sqrt(diffX * diffX + diffY * diffY);
        }

        // converts degrees into radians...
        private static double ToRadians(double degrees)
        {
            return degrees * (Math.PI / 180.0);
        }
    }
}
