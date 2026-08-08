using HololensAirplaneViewer.Models;
using Xunit;

namespace HololensAirplaneViewer.Tests
{
    /// <summary>
    /// Tests for the AirplaneState data model (display naming, position and
    /// altitude resolution). Pure model — no UWP dependencies.
    /// </summary>
    public class AirplaneStateTests
    {
        [Fact]
        public void HasPosition_True_WhenLatAndLonSet()
        {
            var plane = new AirplaneState
            {
                Latitude = 59.91,
                Longitude = 10.75
            };

            Assert.True(plane.HasPosition);
        }

        [Fact]
        public void HasPosition_False_WhenLatMissing()
        {
            var plane = new AirplaneState { Longitude = 10.75 };
            Assert.False(plane.HasPosition);
        }

        [Fact]
        public void HasPosition_False_WhenLonMissing()
        {
            var plane = new AirplaneState { Latitude = 59.91 };
            Assert.False(plane.HasPosition);
        }

        [Fact]
        public void HasPosition_False_WhenBothMissing()
        {
            Assert.False(new AirplaneState().HasPosition);
        }

        [Fact]
        public void AltMeters_PrefersGeometricAltitude()
        {
            var plane = new AirplaneState
            {
                BaroAltitude = 5000f,
                GeoAltitude = 5200f
            };

            Assert.Equal(5200f, plane.AltMeters);
        }

        [Fact]
        public void AltMeters_FallsBackToBaroAltitude()
        {
            var plane = new AirplaneState { BaroAltitude = 5000f };
            Assert.Equal(5000f, plane.AltMeters);
        }

        [Fact]
        public void AltMeters_Zero_WhenNoAltitude()
        {
            Assert.Equal(0f, new AirplaneState().AltMeters);
        }

        [Fact]
        public void DisplayName_UsesCallsign_WhenPresent()
        {
            var plane = new AirplaneState { Callsign = " SAS1234 " };
            Assert.Equal("SAS1234", plane.DisplayName);
        }

        [Fact]
        public void DisplayName_FallsBackToIcao_WhenNoCallsign()
        {
            var plane = new AirplaneState { Icao24 = "4caa77", Callsign = "" };
            Assert.Equal("4CAA77", plane.DisplayName); // model uppercases ICAO
        }
    }
}
