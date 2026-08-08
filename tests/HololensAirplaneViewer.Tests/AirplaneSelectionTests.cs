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

        [Fact]
        public void Select_NearbyGroundTraffic_RanksBeforeAirborne()
        {
            // SAS1314 scenario: an aircraft on the ground 0.9 km from the
            // observer (Gardermoen) must not be starved out by 15+ airborne
            // planes. It should rank in the reserved nearby-ground group.
            var states = new List<AirplaneState>
            {
                Plane("gnd-near", OsloLat + 0.01, OsloLon, baroAlt: 0f, onGround: true),
                Plane("air-1", OsloLat + 0.5, OsloLon, baroAlt: 12000f),
                Plane("air-2", OsloLat + 0.5, OsloLon, baroAlt: 11000f),
                Plane("gnd-far", OsloLat + 2.0, OsloLon, baroAlt: 0f, onGround: true)
            };

            var result = AirplaneSelection.Select(states, 3, OsloLat, OsloLon, 15.0);

            // Reserved nearby-ground slot is filled first, then airborne
            Assert.Equal("gnd-near", result[0].Icao24);
            Assert.Equal("air-1", result[1].Icao24);
            Assert.Equal("air-2", result[2].Icao24);
        }

        [Fact]
        public void Select_NearbyGroundTraffic_ClosestFirst()
        {
            var states = new List<AirplaneState>
            {
                Plane("gnd-2km", OsloLat + 0.02, OsloLon, baroAlt: 0f, onGround: true),
                Plane("gnd-1km", OsloLat + 0.01, OsloLon, baroAlt: 0f, onGround: true)
            };

            var result = AirplaneSelection.Select(states, 2, OsloLat, OsloLon, 15.0);

            Assert.Equal("gnd-1km", result[0].Icao24);
            Assert.Equal("gnd-2km", result[1].Icao24);
        }

        [Fact]
        public void Select_NearbyGroundGroup_IsCapped()
        {
            var states = new List<AirplaneState>();
            for (int i = 0; i < 10; i++)
                states.Add(Plane($"gnd{i:D2}", OsloLat + 0.001 * i, OsloLon,
                    baroAlt: 0f, onGround: true));

            var result = AirplaneSelection.Select(states, 15, OsloLat, OsloLon, 15.0);

            // At most MaxNearbyGroundAircraft ground planes survive
            Assert.True(result.Count <= AirplaneSelection.MaxNearbyGroundAircraft);
        }

        [Fact]
        public void Select_WithoutObserver_KeepsLegacyAirborneFirstOrder()
        {
            // Backward compatibility: no observer/radius → old behavior,
            // airborne first, then on-ground by altitude.
            var states = new List<AirplaneState>
            {
                Plane("gnd", OsloLat, OsloLon, baroAlt: 100f, onGround: true),
                Plane("air", OsloLat, OsloLon, baroAlt: 10000f)
            };

            var result = AirplaneSelection.Select(states, 15);

            Assert.Equal("air", result[0].Icao24);
            Assert.Equal("gnd", result[1].Icao24);
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
