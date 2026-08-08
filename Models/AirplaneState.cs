using System;

namespace HololensAirplaneViewer.Models
{
    /// <summary>
    /// Represents a live aircraft state vector from the OpenSky Network API.
    /// Maps to index 0–5, 7, 9 from the OpenSky states[] array.
    /// </summary>
    public class AirplaneState
    {
        /// <summary>ICAO 24-bit transponder address (e.g. "a47d2b")</summary>
        public string Icao24 { get; set; } = "";

        /// <summary>Callsign (8 chars, e.g. "SAS487"). Null if unknown.</summary>
        public string Callsign { get; set; } = "";

        /// <summary>Country inferred from ICAO prefix (e.g. "Norway")</summary>
        public string OriginCountry { get; set; } = "";

        /// <summary>WGS-84 longitude in decimal degrees. Can be null.</summary>
        public double? Longitude { get; set; }

        /// <summary>WGS-84 latitude in decimal degrees. Can be null.</summary>
        public double? Latitude { get; set; }

        /// <summary>Barometric altitude in meters. Can be null.</summary>
        public float? BaroAltitude { get; set; }

        /// <summary>True track in degrees (0=north, clockwise). Can be null.</summary>
        public float? TrueTrack { get; set; }

        /// <summary>Velocity over ground in m/s. Can be null.</summary>
        public float? Velocity { get; set; }

        /// <summary>Geometric altitude in meters. Can be null.</summary>
        public float? GeoAltitude { get; set; }

        /// <summary>Whether the position came from a surface position report.</summary>
        public bool OnGround { get; set; }

        /// <summary>Whether this aircraft has a valid GPS fix (can be positioned).</summary>
        public bool HasPosition => Latitude.HasValue && Longitude.HasValue;

        /// <summary>Best available altitude in meters (geometric preferred, barometric fallback).</summary>
        public float AltMeters => GeoAltitude ?? BaroAltitude ?? 0f;

        /// <summary>Display name: callsign if set, otherwise ICAO.</summary>
        public string DisplayName => !string.IsNullOrEmpty(Callsign)
            ? Callsign.Trim()
            : Icao24.ToUpperInvariant();
    }
}