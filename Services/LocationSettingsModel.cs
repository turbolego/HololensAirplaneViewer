using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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

    public sealed class AirportLocation
    {
        public AirportLocation(string iata, string name, double latitude, double longitude)
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

        /// <summary>
        /// Parses the bundled tab-separated OurAirports catalog.
        /// </summary>
        public static AirportLocation[] ParseAirportCatalog(IEnumerable<string> lines)
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));

            var airports = new List<AirportLocation>();
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                string[] fields = line.Split('\t');
                if (fields.Length == 4 && fields[0] == "iata")
                    continue;
                if (fields.Length != 4)
                    throw new FormatException("An airport catalog row must contain four tab-separated fields.");

                double latitude;
                double longitude;
                if (!double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out latitude) ||
                    !double.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out longitude) ||
                    latitude < -90.0 || latitude > 90.0 ||
                    longitude < -180.0 || longitude > 180.0 ||
                    string.IsNullOrWhiteSpace(fields[0]) ||
                    string.IsNullOrWhiteSpace(fields[1]))
                {
                    throw new FormatException("An airport catalog row contains invalid airport data.");
                }

                airports.Add(new AirportLocation(fields[0], fields[1], latitude, longitude));
            }

            return airports.ToArray();
        }

        /// <summary>Returns the three catalog airports nearest to the supplied coordinates.</summary>
        public static AirportLocation[] FindThreeClosestAirports(
            IEnumerable<AirportLocation> airports,
            double latitude,
            double longitude)
        {
            if (airports == null) throw new ArgumentNullException(nameof(airports));

            return airports
                .OrderBy(airport => HaversineMeters(latitude, longitude, airport.Latitude, airport.Longitude))
                .Take(3)
                .ToArray();
        }

        private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadiusMeters = 6371000.0;
            double phi1 = lat1 * Math.PI / 180.0;
            double phi2 = lat2 * Math.PI / 180.0;
            double deltaPhi = (lat2 - lat1) * Math.PI / 180.0;
            double deltaLambda = (lon2 - lon1) * Math.PI / 180.0;
            double a = Math.Sin(deltaPhi / 2.0) * Math.Sin(deltaPhi / 2.0) +
                       Math.Cos(phi1) * Math.Cos(phi2) *
                       Math.Sin(deltaLambda / 2.0) * Math.Sin(deltaLambda / 2.0);
            return earthRadiusMeters * 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        }

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
