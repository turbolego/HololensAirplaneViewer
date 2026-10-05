using System.Linq;
using HololensAirplaneViewer.Services;
using Xunit;

namespace HololensAirplaneViewer.Tests
{
    public class LocationSettingsModelTests
    {
        [Fact]
        public void Presets_StartWithDeviceLocationEntry()
        {
            Assert.True(LocationSettingsModel.Presets[0].IsDeviceLocation);
            Assert.Single(LocationSettingsModel.Presets, p => p.IsDeviceLocation);
        }

        [Fact]
        public void Presets_UseValidCoordinates()
        {
            foreach (var preset in LocationSettingsModel.Presets.Where(p => !p.IsDeviceLocation))
            {
                Assert.InRange(preset.Latitude, -90.0, 90.0);
                Assert.InRange(preset.Longitude, -180.0, 180.0);
                Assert.False(string.IsNullOrWhiteSpace(preset.Name));
            }
        }

        [Fact]
        public void NextPresetIndex_WrapsAtTheEnd()
        {
            int last = LocationSettingsModel.Presets.Count - 1;
            Assert.Equal(1, LocationSettingsModel.NextPresetIndex(0));
            Assert.Equal(0, LocationSettingsModel.NextPresetIndex(last));
        }

        [Fact]
        public void AdjustLatitude_ClampsAtThePoles()
        {
            Assert.Equal(90.0, LocationSettingsModel.AdjustLatitude(85.0, 10.0));
            Assert.Equal(-90.0, LocationSettingsModel.AdjustLatitude(-85.0, -10.0));
        }

        [Fact]
        public void AdjustLatitude_AddsDeltaWithoutFloatingPointNoise()
        {
            double value = 59.9;
            for (int i = 0; i < 3; i++)
            {
                value = LocationSettingsModel.AdjustLatitude(value, 0.1);
            }

            Assert.Equal(60.2, value);
        }

        [Fact]
        public void AdjustLongitude_WrapsAcrossTheAntimeridian()
        {
            Assert.Equal(-175.0, LocationSettingsModel.AdjustLongitude(179.0, 6.0));
            Assert.Equal(175.0, LocationSettingsModel.AdjustLongitude(-179.0, -6.0));
        }

        [Fact]
        public void AdjustCoordinates_SupportsNegativeValuesAtFourDecimalPlaces()
        {
            Assert.Equal(-12.3456, LocationSettingsModel.AdjustLatitude(0.0, -12.3456));
            Assert.Equal(-123.4567, LocationSettingsModel.AdjustLongitude(0.0, -123.4567));
        }

        [Fact]
        public void FormatCoordinates_UsesHemisphereSuffixes()
        {
            Assert.Equal("59.9139° N, 10.7522° E", LocationSettingsModel.FormatCoordinates(59.9139, 10.7522));
            Assert.Equal("33.8688° S, 74.0060° W", LocationSettingsModel.FormatCoordinates(-33.8688, -74.006));
        }

        [Fact]
        public void FormatStep_IsCompact()
        {
            Assert.Equal("10°", LocationSettingsModel.FormatStep(10.0));
            Assert.Equal("0.1°", LocationSettingsModel.FormatStep(0.1));
            Assert.Equal("0.0001°", LocationSettingsModel.FormatStep(0.0001));
        }

        [Theory]
        [InlineData("59.9,10.7", 59.9, 10.7)]
        [InlineData("-33.8688;151.2093", -33.8688, 151.2093)]
        public void TryParseCoordinateString_ParsesExactlyTwoValidCoordinates(
            string input,
            double expectedLatitude,
            double expectedLongitude)
        {
            Assert.True(LocationSettingsModel.TryParseCoordinateString(input, out double latitude, out double longitude));
            Assert.Equal(expectedLatitude, latitude);
            Assert.Equal(expectedLongitude, longitude);
        }

        [Theory]
        [InlineData("59.9,10.7,extra")]
        [InlineData("59.9 10.7 extra")]
        [InlineData("59.9;10.7;extra")]
        [InlineData("59.9")]
        public void TryParseCoordinateString_RejectsOtherThanTwoCoordinates(string input)
        {
            Assert.False(LocationSettingsModel.TryParseCoordinateString(input, out _, out _));
        }

        [Fact]
        public void NorwegianAirportExamples_AreExplicitlyRegional()
        {
            Assert.Equal(new[] { "OSL", "BGO", "TRD" }, LocationSettingsModel.NorwegianAirportExamples.Select(a => a.Iata));
        }

        [Fact]
        public void StepSizes_AreDescendingAndPositiveThroughFourDecimalPlaces()
        {
            var steps = LocationSettingsModel.StepSizesDegrees;
            Assert.Equal(6, steps.Length);
            for (int i = 0; i < steps.Length; i++)
            {
                Assert.True(steps[i] > 0.0);
                if (i > 0)
                {
                    Assert.True(steps[i] < steps[i - 1]);
                }
            }
        }
    }

    public class LocationOverrideStoreTests
    {
        [Fact]
        public void SetThenClear_RestoresAutomaticLocationAndBumpsGeneration()
        {
            LocationOverrideStore.Clear();
            int start = LocationOverrideStore.GetGeneration();

            LocationOverrideStore.Set(59.9139, 10.7522);

            double latitude;
            double longitude;
            Assert.True(LocationOverrideStore.TryGet(out latitude, out longitude));
            Assert.Equal(59.9139, latitude);
            Assert.Equal(10.7522, longitude);
            Assert.Equal(start + 1, LocationOverrideStore.GetGeneration());

            LocationOverrideStore.Clear();

            Assert.False(LocationOverrideStore.TryGet(out latitude, out longitude));
            Assert.Equal(start + 2, LocationOverrideStore.GetGeneration());

            // Clearing when no override is active must not bump the generation,
            // otherwise in-flight fetches would be discarded needlessly.
            LocationOverrideStore.Clear();
            Assert.Equal(start + 2, LocationOverrideStore.GetGeneration());
        }
    }
}
