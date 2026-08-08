using System;
using System.Numerics;

namespace HololensAirplaneViewer.Services
{
    /// <summary>
    /// Pure geodetic math for mapping live aircraft positions into the
    /// HoloLens local dome, plus line-of-sight (horizon) visibility checks.
    ///
    /// This class has NO UWP/SharpDX dependencies so it can be unit-tested
    /// in a plain .NET test project (see tests/HololensAirplaneViewer.Tests).
    /// </summary>
    public static class AirplaneMath
    {
        /// <summary>Mean Earth radius in meters (WGS-84).</summary>
        public const double EarthRadiusMeters = 6371000.0;

        /// <summary>Meters per degree of latitude (planar approximation).</summary>
        public const double OneDegreeMeters = 111320.0;

        /// <summary>Default observer height above the GPS fix (HoloLens at head height).</summary>
        public const double DefaultObserverAltitudeMeters = 1.5;

        /// <summary>Planar dome radius the aircraft are projected onto (meters).</summary>
        public const float DomeRadiusMeters = 2.5f;

        /// <summary>Vertical gap between the dome ceiling and the markers.</summary>
        public const float CeilingClearance = 0.3f;

        /// <summary>Scales real altitude (m) into dome display units.</summary>
        public const double AltitudeDisplayScale = 0.002;

        /// <summary>Cap for the altitude-driven vertical offset in the dome.</summary>
        public const double AltitudeDisplayCap = 1.5;

        /// <summary>Markers never sink below this floor relative to the user.</summary>
        public const float FloorMargin = 0.1f;

        public static double DegreesToRadians(double deg) => deg * Math.PI / 180.0;

        /// <summary>
        /// Great-circle distance between two WGS-84 points (haversine, meters).
        /// </summary>
        public static double GreatCircleDistanceMeters(
            double lat1Deg, double lon1Deg, double lat2Deg, double lon2Deg)
        {
            double lat1 = DegreesToRadians(lat1Deg);
            double lat2 = DegreesToRadians(lat2Deg);
            double dLat = DegreesToRadians(lat2Deg - lat1Deg);
            double dLon = DegreesToRadians(lon2Deg - lon1Deg);

            double a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0)
                     + Math.Cos(lat1) * Math.Cos(lat2)
                     * Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);
            double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(Math.Max(0.0, 1.0 - a)));
            return EarthRadiusMeters * c;
        }

        /// <summary>
        /// Maximum line-of-sight distance to a target at <paramref name="targetAltM"/>
        /// seen from an observer at <paramref name="observerAltM"/> (both meters above
        /// the WGS-84 ellipsoid), ignoring atmospheric refraction:
        ///   d = sqrt(2·R·h_obs) + sqrt(2·R·h_target)
        /// </summary>
        public static double HorizonDistanceMeters(double observerAltM, double targetAltM)
        {
            double obs = Math.Max(0.0, observerAltM);
            double tgt = Math.Max(0.0, targetAltM);
            return Math.Sqrt(2.0 * EarthRadiusMeters * obs)
                 + Math.Sqrt(2.0 * EarthRadiusMeters * tgt);
        }

        /// <summary>
        /// True when the target aircraft is above the Earth's horizon from the
        /// observer's GPS position — i.e. the straight line between them clears
        /// the Earth's curvature. A target below the horizon is physically not
        /// visible and should not be rendered.
        /// </summary>
        public static bool IsAboveHorizon(
            double observerLatDeg, double observerLonDeg, double observerAltM,
            double targetLatDeg, double targetLonDeg, double targetAltM)
        {
            double distance = GreatCircleDistanceMeters(
                observerLatDeg, observerLonDeg, targetLatDeg, targetLonDeg);
            double horizon = HorizonDistanceMeters(observerAltM, targetAltM);
            return distance <= horizon;
        }

        /// <summary>
        /// Maps a live aircraft's WGS-84 lat/lon/alt into local HoloLens world
        /// coordinates inside the dome above the user's GPS fix.
        ///
        /// Planar approximation around the fix:
        ///   1° latitude  ≈ 111,320 m
        ///   1° longitude ≈ 111,320 m × cos(lat)
        /// Horizontal offset is clamped into the dome radius; the real altitude
        /// is scaled into a vertical display offset (capped).
        /// </summary>
        public static Vector3 ComputeDomePosition(
            double planeLatDeg, double planeLonDeg, double planeAltM,
            double observerLatDeg, double observerLonDeg,
            Vector3 worldCenter, float ceilingY)
        {
            double dLat = planeLatDeg - observerLatDeg;
            double dLon = planeLonDeg - observerLonDeg;

            double xMeters = dLon * OneDegreeMeters * Math.Cos(DegreesToRadians(observerLatDeg));
            double zMeters = dLat * OneDegreeMeters;

            // Altitude meters → display units (scaled down for the dome, capped)
            double altDisplay = Math.Min(Math.Max(planeAltM, 0.0) * AltitudeDisplayScale, AltitudeDisplayCap);

            // Clamp horizontal offset into the dome radius
            double horiz = Math.Min(
                Math.Sqrt(xMeters * xMeters + zMeters * zMeters),
                DomeRadiusMeters - 0.2);
            double angle = Math.Atan2(zMeters, xMeters);

            float x = (float)(horiz * Math.Cos(angle));
            float z = (float)(horiz * Math.Sin(angle));
            float y = (ceilingY - CeilingClearance) + (float)altDisplay;

            // Keep markers above the floor relative to the user
            if (y < worldCenter.Y - FloorMargin)
            {
                y = worldCenter.Y - FloorMargin;
            }

            return new Vector3(worldCenter.X + x, y, worldCenter.Z + z);
        }
    }
}
