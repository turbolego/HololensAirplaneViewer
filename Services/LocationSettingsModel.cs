using System;
using System.Collections.Generic;
using System.Globalization;

namespace HololensAirplaneViewer.Services
{
    /// <summary>
    /// A selectable location offered by the settings modal. The entry with
    /// <see cref="IsDeviceLocation"/> set returns the app to the automatic
    /// (device supplied) position instead of a fixed coordinate.
    /// </summary>
    public sealed class LocationPreset
    {
        public LocationPreset(string name, double latitude, double longitude)
        {
            Name = name;
            Latitude = latitude;
            Longitude = longitude;
        }

        private LocationPreset(string name)
        {
            Name = name;
            IsDeviceLocation = true;
        }

        public string Name { get; private set; }
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }
        public bool IsDeviceLocation { get; private set; }

        public static LocationPreset DeviceLocation(string name)
        {
            return new LocationPreset(name);
        }
    }

    public sealed class AirportExample
    {
        public AirportExample(string iata, string name, double latitude, double longitude)
        {
            Iata = iata;
            Name = name;
            Latitude = latitude;
            Longitude = longitude;
        }

        public string Iata { get; private set; }
        public string Name { get; private set; }
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }
    }

    /// <summary>
    /// Pure (UWP-free) logic behind the holographic location settings modal.
    /// The modal is built from <see cref="Windows.UI.Popups.MessageDialog"/>
    /// instances, which only support command buttons, so the location is
    /// changed by picking a preset or by stepping the coordinates.
    /// </summary>
    public static class LocationSettingsModel
    {
        /// <summary>Step sizes, in degrees, offered when nudging a coordinate.</summary>
        public static readonly double[] StepSizesDegrees = { 10.0, 1.0, 0.1, 0.01, 0.001, 0.0001 };

        private static readonly LocationPreset[] PresetList =
        {
            LocationPreset.DeviceLocation("Device location (automatic)"),
            new LocationPreset("Oslo, Norway", 59.9139, 10.7522),
            new LocationPreset("London, United Kingdom", 51.5074, -0.1278),
            new LocationPreset("Amsterdam, Netherlands", 52.3676, 4.9041),
            new LocationPreset("Frankfurt, Germany", 50.1109, 8.6821),
            new LocationPreset("Paris, France", 48.8566, 2.3522),
            new LocationPreset("New York, USA", 40.7128, -74.0060),
            new LocationPreset("Los Angeles, USA", 34.0522, -118.2437),
            new LocationPreset("Dubai, UAE", 25.2048, 55.2708),
            new LocationPreset("Singapore", 1.3521, 103.8198),
            new LocationPreset("Tokyo, Japan", 35.6762, 139.6503),
            new LocationPreset("Sydney, Australia", -33.8688, 151.2093),
        };

        public static IList<LocationPreset> Presets
        {
            get { return PresetList; }
        }

        /// <summary>Airport examples in Norway (IATA code, name, lat, lon).</summary>
        public static readonly AirportExample[] NorwegianAirportExamples =
        {
            new AirportExample("OSL", "Oslo Airport Gardermoen", 60.1939, 11.1004),
            new AirportExample("BGO", "Bergen Airport Flesland", 60.2934, 5.2192),
            new AirportExample("TRD", "Trondheim Airport Værnes", 63.4578, 10.9241),
        };

        /// <summary>Cycles to the next preset, wrapping at the end of the list.</summary>
        public static int NextPresetIndex(int index)
        {
            if (PresetList.Length == 0)
            {
                return 0;
            }

            int next = index + 1;
            return next >= PresetList.Length ? 0 : next;
        }

        /// <summary>Applies a latitude delta, clamped to the valid pole range.</summary>
        public static double AdjustLatitude(double latitude, double deltaDegrees)
        {
            double adjusted = latitude + deltaDegrees;
            if (adjusted > 90.0) return 90.0;
            if (adjusted < -90.0) return -90.0;
            return Round(adjusted);
        }

        /// <summary>Applies a longitude delta, wrapping across the antimeridian.</summary>
        public static double AdjustLongitude(double longitude, double deltaDegrees)
        {
            double adjusted = longitude + deltaDegrees;
            while (adjusted > 180.0) adjusted -= 360.0;
            while (adjusted <= -180.0) adjusted += 360.0;
            return Round(adjusted);
        }

        /// <summary>
        /// Rounds away accumulated floating point noise from repeated 0.1°
        /// steps so the modal shows clean values such as 59.9 instead of
        /// 59.89999999999999.
        /// </summary>
        private static double Round(double value)
        {
            return Math.Round(value, 4, MidpointRounding.AwayFromZero);
        }

        public static string FormatCoordinates(double latitude, double longitude)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:F4}° {1}, {2:F4}° {3}",
                Math.Abs(latitude),
                latitude >= 0.0 ? "N" : "S",
                Math.Abs(longitude),
                longitude >= 0.0 ? "E" : "W");
        }

        public static string FormatStep(double stepDegrees)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.####}°", stepDegrees);
        }

        /// <summary>
        /// Parse a decimal lat/lon string "lat,lon" into doubles.
        /// Returns false on parse failure.
        /// </summary>
        public static bool TryParseCoordinateString(string input, out double latitude, out double longitude)
        {
            latitude = 0; longitude = 0;
            if (string.IsNullOrWhiteSpace(input)) return false;
            var parts = input.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return false;
            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out latitude)) return false;
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out longitude)) return false;
            if (latitude < -90 || latitude > 90) return false;
            if (longitude < -180 || longitude > 180) return false;
            return true;
        }

        /// <summary>
        /// Decode a geohash to lat/lon using GeohashConverter.
        /// Returns false on decode failure.
        /// </summary>
        public static bool TryDecodeGeohash(string geohash, out double latitude, out double longitude)
        {
            latitude = 0; longitude = 0;
            try
            {
                var coord = Utilities.GeohashConverter.Decode(geohash);
                latitude = coord.Latitude;
                longitude = coord.Longitude;
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
