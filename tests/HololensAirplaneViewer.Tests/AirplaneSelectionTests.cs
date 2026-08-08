using System.Collections.Generic;
using HololensAirplaneViewer.Models;
using HololensAirplaneViewer.Services;
using Xunit;

namespace HololensAirplaneViewer.Tests
{
    /// <summary>
    /// Tests for the pure aircraft selection/filtering logic used by
    /// AirplaneService and AirplaneRenderer.
    /// </summary>
    public class AirplaneSelectionTests
    {
        private const double OsloLat = 59.91;
        private const double OsloLon = 10.75;

        private static AirplaneState Plane(
            string icao, double lat, double lon, float? baroAlt = 10000f,
            bool onGround = false, float? geoAlt = null)
        {
            return new AirplaneState
            {
                Icao24 = icao,
                Callsign = icao.ToUpperInvariant(),
                Latitude = lat,
                Longitude = lon,
                BaroAltitude = baroAlt,
                GeoAltitude = geoAlt,
                OnGround = onGround
            };
        }

        // ------------------------------------------------------------------
        // Select (sorting / filtering / cap)
        // ------------------------------------------------------------------

        [Fact]
        public void Select_PlanesWithoutPosition_AreExcluded()
        {
            var states = new List<AirplaneState>
            {
                Plane("abc111", OsloLat, OsloLon),          // has position
                new AirplaneState { Icao24 = "abc222" }      // no position
            };

            var result = AirplaneSelection.Select(states, 15);

            Assert.Single(result);
            Assert.Equal("abc111", result[0].Icao24);
        }

        [Fact]
        public void Select_AirborneBeforeOnGround()
        {
            var states = new List<AirplaneState>
            {
                Plane("on-gnd", OsloLat, OsloLon, baroAlt: 100f, onGround: true),
                Plane("air-01", OsloLat, OsloLon, baroAlt: 10000f)
            };

            var result = AirplaneSelection.Select(states, 15);

            Assert.Equal("air-01", result[0].Icao24);
            Assert.Equal("on-gnd", result[1].Icao24);
        }

        [Fact]
        public void Select_WithinSameGroundStatus_HighestAltitudeFirst()
        {
            var states = new List<AirplaneState>
            {
                Plane("low", OsloLat, OsloLon, baroAlt: 3000f),
                Plane("high", OsloLat, OsloLon, baroAlt: 12000f),
                Plane("mid", OsloLat, OsloLon, baroAlt: 8000f)
            };

            var result = AirplaneSelection.Select(states, 15);

            Assert.Equal(new[] { "high", "mid", "low" }, result.ConvertAll(s => s.Icao24));
        }

        [Fact]
        public void Select_RespectsMaxCount()
        {
            var states = new List<AirplaneState>();
            for (int i = 0; i < 20; i++)
                states.Add(Plane($"ac{i:D3}", OsloLat, OsloLon, baroAlt: 10000f - i));

            var result = AirplaneSelection.Select(states, 5);

            Assert.Equal(5, result.Count);
        }

        [Fact]
        public void Select_NullBaroAltitude_SortsAsZero()
        {
            var states = new List<AirplaneState>
            {
                Plane("null-alt", OsloLat, OsloLon, baroAlt: null),
                Plane("low", OsloLat, OsloLon, baroAlt: 2000f)
            };

            var result = AirplaneSelection.Select(states, 15);

            Assert.Equal("low", result[0].Icao24);
            Assert.Equal("null-alt", result[1].Icao24);
        }

        // ------------------------------------------------------------------
        // OnlyVisibleFrom (line of sight)
        // ------------------------------------------------------------------

        [Fact]
        public void OnlyVisibleFrom_OverheadPlane_IsKept()
        {
            var states = new List<AirplaneState>
            {
                Plane("overhead", OsloLat, OsloLon, baroAlt: 10000f)
            };

            var result = AirplaneSelection.OnlyVisibleFrom(states, OsloLat, OsloLon, 1.5);

            Assert.Single(result);
        }

        [Fact]
        public void OnlyVisibleFrom_BelowHorizonPlane_IsDropped()
        {
            // 500 m altitude ~90 km north: below the horizon (≈80 km) → dropped
            var states = new List<AirplaneState>
            {
                Plane("hidden", OsloLat + 0.81, OsloLon, baroAlt: 500f)
            };

            var result = AirplaneSelection.OnlyVisibleFrom(states, OsloLat, OsloLon, 1.5);

            Assert.Empty(result);
        }

        [Fact]
        public void OnlyVisibleFrom_HighAndLow_KeepsOnlyVisible()
        {
            var states = new List<AirplaneState>
            {
                Plane("high-visible", OsloLat + 0.81, OsloLon, baroAlt: 10000f),  // visible (357 km)
                Plane("low-hidden", OsloLat + 0.81, OsloLon, baroAlt: 500f)       // hidden (80 km)
            };

            var result = AirplaneSelection.OnlyVisibleFrom(states, OsloLat, OsloLon, 1.5);

            Assert.Single(result);
            Assert.Equal("high-visible", result[0].Icao24);
        }

        [Fact]
        public void OnlyVisibleFrom_PositionlessPlane_IsDropped()
        {
            var states = new List<AirplaneState>
            {
                new AirplaneState { Icao24 = "no-pos" }
            };

            var result = AirplaneSelection.OnlyVisibleFrom(states, OsloLat, OsloLon, 1.5);

            Assert.Empty(result);
        }
    }
}
