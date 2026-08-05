using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HololensAirplaneViewer.Models;
using Windows.Data.Json;

namespace HololensAirplaneViewer.Services
{
    /// <summary>
    /// Fetches live aircraft state vectors from the OpenSky Network REST API.
    /// Uses anonymous access: 400 credits/day, 10-second resolution.
    /// No authentication required. Non-commercial use only.
    ///
    /// API docs: https://openskynetwork.github.io/opensky-api/rest.html
    /// JSON parsing uses Windows.Data.Json (UWP built-in, no extra NuGet).
    /// </summary>
    public class AirplaneService
    {
        private const string StatesUrl = "https://opensky-network.org/api/states/all";

        private static readonly HttpClient HttpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        /// <summary>
        /// Returns up to maxCount aircraft that currently have valid GPS positions,
        /// sorted airborne first then by highest altitude.
        /// </summary>
        public async Task<List<AirplaneState>> GetLiveStatesAsync(
            double? lamin = null, double? lomin = null,
            double? lamax = null, double? lomax = null,
            int maxCount = 15)
        {
            var url = StatesUrl;

            if (lamin.HasValue && lomin.HasValue && lamax.HasValue && lomax.HasValue)
            {
                url += string.Format(
                    CultureInfo.InvariantCulture,
                    "?lamin={0}&lomin={1}&lamax={2}&lomax={3}",
                    lamin.Value, lomin.Value, lamax.Value, lomax.Value);
            }

            try
            {
                var raw = await HttpClient.GetStringAsync(url);
                return ParseStates(raw, maxCount);
            }
            catch
            {
                return new List<AirplaneState>();
            }
        }

        /// <summary>
        /// Parses the OpenSky "states/all" JSON response.
        /// Each state vector is a flat JSON array:
        /// [0]=icao24, [1]=callsign, [2]=origin_country, [5]=longitude,
        /// [6]=latitude, [7]=baro_altitude, [8]=on_ground, [9]=velocity,
        /// [10]=true_track, [13]=geo_altitude, [14]=vertical_rate, [15]=sensors
        /// </summary>
        private static List<AirplaneState> ParseStates(string json, int maxCount)
        {
            var list = new List<AirplaneState>();

            try
            {
                var root = JsonObject.Parse(json);
                if (!root.ContainsKey("states"))
                    return list;

                var states = root["states"].GetArray();

                foreach (var entry in states)
                {
                    if (entry.ValueType != JsonValueType.Array)
                        continue;

                    var arr = entry.GetArray();
                    if (arr.Count < 18)
                        continue;

                    // Skip aircraft without position
                    double? lon = SafeGetDouble(arr, 5);
                    double? lat = SafeGetDouble(arr, 6);
                    if (!lon.HasValue || !lat.HasValue)
                        continue;

                    var plane = new AirplaneState
                    {
                        Icao24 = SafeGetString(arr, 0) ?? "?",
                        Callsign = SafeGetString(arr, 1)?.Trim() ?? "",
                        OriginCountry = SafeGetString(arr, 2) ?? "",
                        Longitude = lon,
                        Latitude = lat,
                        BaroAltitude = SafeGetFloat(arr, 7),
                        OnGround = SafeGetBool(arr, 8),
                        Velocity = SafeGetFloat(arr, 9),
                        TrueTrack = SafeGetFloat(arr, 10),
                        GeoAltitude = SafeGetFloat(arr, 13),
                    };

                    list.Add(plane);

                    if (list.Count >= maxCount * 3)
                        break;
                }
            }
            catch
            {
                // Gracefully return what we parsed so far
            }

            return list
                .Where(a => a.HasPosition)
                .OrderBy(a => a.OnGround ? 1 : 0)
                .ThenByDescending(a => a.BaroAltitude ?? 0)
                .Take(maxCount)
                .ToList();
        }

        private static string SafeGetString(JsonArray arr, int idx)
        {
            if (idx >= arr.Count) return null;
            var v = arr[idx];
            return v.ValueType == JsonValueType.Null ? null : v.GetString();
        }

        private static double? SafeGetDouble(JsonArray arr, int idx)
        {
            if (idx >= arr.Count) return null;
            var v = arr[idx];
            if (v.ValueType == JsonValueType.Null) return null;
            try { return v.GetNumber(); } catch { return null; }
        }

        private static float? SafeGetFloat(JsonArray arr, int idx)
        {
            var d = SafeGetDouble(arr, idx);
            return d.HasValue ? (float)d.Value : (float?)null;
        }

        private static bool SafeGetBool(JsonArray arr, int idx)
        {
            if (idx >= arr.Count) return false;
            var v = arr[idx];
            return v.ValueType != JsonValueType.Null && v.GetBoolean();
        }
    }
}