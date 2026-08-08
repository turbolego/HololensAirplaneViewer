using System;
using System.Numerics;
using HololensAirplaneViewer.Services;
using Xunit;

namespace HololensAirplaneViewer.Tests
{
    /// <summary>
    /// Tests for the pure geodetic math: dome position mapping (the exact
    /// algorithm used by AirplaneRenderer.ComputeAirplanePosition) and the
    /// line-of-sight / horizon visibility checks.
    ///
    /// Reference frame (Oslo, Norway):
    ///   lat 59.91°, lon 10.75°  — the app's default GPS fallback.
    /// At this latitude 1° of longitude ≈ 55,800 m (111,320 × cos(59.91°)).
    /// </summary>
    public class AirplaneMathTests
    {
        private const double OsloLat = 59.91;
        private const double OsloLon = 10.75;
        private static readonly Vector3 Origin = Vector3.Zero;
        private const float CeilingY = 1.4f; // worldCenter.Y + CeilingOffset(1.4)

        // ------------------------------------------------------------------
        // Great-circle distance
        // ------------------------------------------------------------------

        [Fact]
        public void GreatCircleDistance_IdenticalPoint_IsZero()
        {
            double d = AirplaneMath.GreatCircleDistanceMeters(OsloLat, OsloLon, OsloLat, OsloLon);
            Assert.Equal(0.0, d, 3);
        }

        [Fact]
        public void GreatCircleDistance_OneDegreeNorth_IsAbout111Km()
        {
            double d = AirplaneMath.GreatCircleDistanceMeters(OsloLat, OsloLon, OsloLat + 1.0, OsloLon);
            Assert.Equal(111320.0, d, 300.0); // meridian distance ≈ 111.32 km
        }

        [Fact]
        public void GreatCircleDistance_OneDegreeEast_IsAbout55_8KmAtOslo()
        {
            double d = AirplaneMath.GreatCircleDistanceMeters(OsloLat, OsloLon, OsloLat, OsloLon + 1.0);
            // 111,320 × cos(59.91°) ≈ 55,800 m
            Assert.Equal(55800.0, d, 400.0);
        }

        // ------------------------------------------------------------------
        // Horizon / line of sight
        // ------------------------------------------------------------------

        [Fact]
        public void HorizonDistance_10KmPlane_IsAbout357Km()
        {
            // sqrt(2·R·h) with R = 6,371,000 m, h = 10,000 m ≈ 356,960 m
            double h = AirplaneMath.HorizonDistanceMeters(0.0, 10000.0);
            Assert.Equal(356960.0, h, 200.0);
        }

        [Fact]
        public void HorizonDistance_ObserverAltitude_ExtendsRange()
        {
            double fromGround = AirplaneMath.HorizonDistanceMeters(0.0, 10000.0);
            double fromHead = AirplaneMath.HorizonDistanceMeters(1.5, 10000.0);
            Assert.True(fromHead > fromGround);
        }

        [Fact]
        public void IsAboveHorizon_PlaneDirectlyOverhead_IsVisible()
        {
            Assert.True(AirplaneMath.IsAboveHorizon(
                OsloLat, OsloLon, 1.5, OsloLat, OsloLon, 10000.0));
        }

        [Fact]
        public void IsAboveHorizon_10KmPlane_90KmAway_IsVisible()
        {
            // 90 km north (0.81°) at 10 km altitude: horizon ≈ 357 km → visible
            Assert.True(AirplaneMath.IsAboveHorizon(
                OsloLat, OsloLon, 1.5, OsloLat + 0.81, OsloLon, 10000.0));
        }

        [Fact]
        public void IsAboveHorizon_LowPlane_FarAway_IsHiddenByCurvature()
        {
            // 500 m altitude at ~90 km: horizon ≈ sqrt(2·R·500) ≈ 79.8 km → hidden
            Assert.False(AirplaneMath.IsAboveHorizon(
                OsloLat, OsloLon, 1.5, OsloLat + 0.81, OsloLon, 500.0));
        }

        [Fact]
        public void IsAboveHorizon_10KmPlane_At3DegreeBoxEdge_IsVisible()
        {
            // The app queries ±3° (≈334 km north). 10 km plane horizon ≈ 357 km,
            // so an aircraft at the query-box edge is still physically visible.
            Assert.True(AirplaneMath.IsAboveHorizon(
                OsloLat, OsloLon, 1.5, OsloLat + 3.0, OsloLon, 10000.0));
        }

        [Fact]
        public void IsAboveHorizon_LowPlane_At3DegreeBoxEdge_IsNotVisible()
        {
            // 1 km altitude at 334 km: horizon ≈ 112.9 km → definitely hidden.
            Assert.False(AirplaneMath.IsAboveHorizon(
                OsloLat, OsloLon, 1.5, OsloLat + 3.0, OsloLon, 1000.0));
        }

        [Fact]
        public void IsAboveHorizon_PlaneBelowSeaLevel_NeverVisibleFarAway()
        {
            Assert.False(AirplaneMath.IsAboveHorizon(
                OsloLat, OsloLon, 1.5, OsloLat + 1.0, OsloLon, -100.0));
        }

        // ------------------------------------------------------------------
        // Dome position mapping (parity with AirplaneRenderer)
        // ------------------------------------------------------------------

        [Fact]
        public void ComputeDomePosition_PlaneAtObserverPosition_IsAboveCenter()
        {
            var p = AirplaneMath.ComputeDomePosition(
                OsloLat, OsloLon, 0.0, OsloLat, OsloLon, Origin, CeilingY);

            Assert.Equal(0.0f, p.X, 3);
            Assert.Equal(0.0f, p.Z, 3);
            // y = ceilingY - clearance + 0 = 1.4 - 0.3
            Assert.Equal(1.1f, p.Y, 3);
        }

        [Fact]
        public void ComputeDomePosition_PlaneNorth_ClampsToDomeRadius()
        {
            var p = AirplaneMath.ComputeDomePosition(
                OsloLat + 1.0, OsloLon, 10000.0, OsloLat, OsloLon, Origin, CeilingY);

            // Horizontal offset clamps to domeRadius - 0.2 = 2.3
            double horiz = Math.Sqrt(p.X * p.X + p.Z * p.Z);
            Assert.Equal(2.3, horiz, 3);

            // North is +Z in the dome frame
            Assert.Equal(0.0f, p.X, 3);
            Assert.Equal(2.3f, p.Z, 3);

            // Altitude 10,000 m → scale 0.002 → 20 → capped at 1.5
            Assert.Equal(1.1f + 1.5f, p.Y, 3);
        }

        [Fact]
        public void ComputeDomePosition_PlaneEast_SetsPositiveX()
        {
            var p = AirplaneMath.ComputeDomePosition(
                OsloLat, OsloLon + 1.0, 0.0, OsloLat, OsloLon, Origin, CeilingY);

            Assert.True(p.X > 0.0f);
            Assert.Equal(0.0f, p.Z, 3);
            Assert.Equal(1.1f, p.Y, 3);
        }

        [Fact]
        public void ComputeDomePosition_AltitudeCap_Applied()
        {
            var p = AirplaneMath.ComputeDomePosition(
                OsloLat, OsloLon, 5000.0, OsloLat, OsloLon, Origin, CeilingY);

            // 5000 × 0.002 = 10 → capped to 1.5
            Assert.Equal(1.1f + 1.5f, p.Y, 3);
        }

        [Fact]
        public void ComputeDomePosition_AltitudeScaling_IsLinearBelowCap()
        {
            var p = AirplaneMath.ComputeDomePosition(
                OsloLat, OsloLon, 500.0, OsloLat, OsloLon, Origin, CeilingY);

            // 500 × 0.002 = 1.0
            Assert.Equal(1.1f + 1.0f, p.Y, 3);
        }

        [Fact]
        public void ComputeDomePosition_WorldCenterOffset_IsApplied()
        {
            var center = new Vector3(10.0f, 2.0f, -5.0f);
            float ceiling = 2.0f + 1.4f;

            var p = AirplaneMath.ComputeDomePosition(
                OsloLat, OsloLon, 0.0, OsloLat, OsloLon, center, ceiling);

            Assert.Equal(10.0f, p.X, 3);
            Assert.Equal(-5.0f, p.Z, 3);
            Assert.Equal(2.0f + 1.4f - 0.3f, p.Y, 3);
        }

        [Fact]
        public void ComputeDomePosition_NegativeAltitude_FloorClamp()
        {
            // A low ceiling makes the marker hit the floor clamp:
            // y < worldCenter.Y - 0.1 → y = worldCenter.Y - 0.1
            var p = AirplaneMath.ComputeDomePosition(
                OsloLat, OsloLon, -50.0, OsloLat, OsloLon, Origin, -1.0f);

            Assert.Equal(-0.1f, p.Y, 3);
        }

        // ------------------------------------------------------------------
        // OpenSky API contract
        // ------------------------------------------------------------------

        [Fact]
        public void OpenSkyStateVectorMinimumFields_Is17_Not18()
        {
            // REGRESSION GUARD: the OpenSky states/all API returns exactly 17
            // fields per state vector. A guard of < 18 would reject EVERY
            // aircraft and the app would render nothing.
            Assert.Equal(17, AirplaneMath.OpenSkyStateVectorMinimumFields);
        }

        // ------------------------------------------------------------------
        // App-level behavior: the ±3° query box vs line of sight
        // ------------------------------------------------------------------

        [Fact]
        public void QueryBox_3Degrees_MatchesAppConstants()
        {
            // Sanity: ±3° box north edge ≈ 334 km; matches Update()'s bbox query.
            double boxEdgeKm = AirplaneMath.GreatCircleDistanceMeters(
                OsloLat, OsloLon, OsloLat + 3.0, OsloLon) / 1000.0;
            Assert.InRange(boxEdgeKm, 330.0, 340.0);
        }
    }
}
