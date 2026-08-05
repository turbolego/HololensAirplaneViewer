using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HololensAirplaneViewer.Models;

namespace HololensAirplaneViewer.Services
{
    /// <summary>
    /// Fetches live aircraft state vectors from the OpenSky Network REST API.
    /// Uses anonymous access: 400 credits/day, 10-second resolution.
    /// No authentication required. Non-commercial use only.
    ///
    /// API docs: https://openskynetwork.github.io/opensky-api/rest.html
    /// </summary>
    public class AirplaneService
    {
        // Anonymous, no-auth endpoint — returns ALL currently tracked aircraft.
        // Optionally filtered by bounding box with lamin/lomin/lamax/lomax params.
        private const string StatesUrl = "https://opensky-network.org/api/states/all";

        private static readonly HttpClient HttpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        /// <summary>
        /// Returns up to maxCount aircraft that currently have valid GPS positions,
        /// sorted by closest altitude to the observer (most visually interesting first).
        /// </summary>
        public async Task<List<AirplaneState>> GetLiveStatesAsync(
            double? lamin = null, double? lomin = null,
            double? lamax = null, double? lomax = null,
            int maxCount = 15)
        {
            var url = StatesUrl;

            // Optional bounding box filter
            if (lamin.HasValue && lomin.HasValue && lamax.HasValue && lomax.HasValue)
            {
                url += $"?lamin={lamin.Value.ToString(CultureInfo.InvariantCulture)}"
                     + $"&lomin={lomin.Value.ToString(CultureInfo.InvariantCulture)}"
                     + $"&lamax={lamax.Value.ToString(CultureInfo.InvariantCulture)}"
                     + $"&lomax={lomax.Value.ToString(CultureInfo.InvariantCulture)}";
            }

            try
            {
                var raw = await HttpClient.GetStringAsync(url);
                return ParseStates(raw, maxCount);
            }
            catch
            {
                // Network/serialization error — return empty
                return new List<AirplaneState>();
            }
        }

        private List<AirplaneState> ParseStates(string json, int maxCount)
        {
            var all = new List<AirplaneState>();

            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("states", out var states))
                    return all;

                foreach (var entry in states.EnumerateArray())
                {
                    if (entry.ValueKind != System.Text.Json.JsonValueKind.Array || entry.GetArrayLength() < 18)
                        continue;

                    var arr = entry.EnumerateArray().ToArray();

                    var icao24 = arr[0].GetString();
                    var callsign = arr[1].GetString()?.Trim();
                    var origin = arr[2].GetString();

                    double? lon = TryPropertyDouble(arr, 5);
                    double? lat = TryPropertyDouble(arr, 6);

                    if (!lon.HasValue || !lat.HasValue)
                        continue; // no GPS fix — skip

                    var onGround = arr[8].ValueKind != System.Text.Json.JsonValueKind.Null && arr[8].GetBoolean();

                    all.Add(new AirplaneState
                    {
                        Icao24 = icao24 ?? "?",
                        Callsign = callsign ?? "",
                        OriginCountry = origin ?? "",
                        Longitude = lon,
                        Latitude = lat,
                        BaroAltitude = TryPropertyFloat(arr, 7),
                        TrueTrack = TryPropertyFloat(arr, 10),
                        Velocity = TryPropertyFloat(arr, 9),
                        GeoAltitude = TryPropertyFloat(arr, 13),
                        OnGround = onGround,
                    });

                    if (all.Count >= maxCount * 3)
                        break; // Enough raw entries; we'll filter and sort below
                }
            }
            catch
            {
                // JSON parse mismatch — return what we have
            }

            // Sort by absolute altitude (interesting ones first, de-prioritize ground)
            // Then take maxCount
            return all
                .Where(a => a.HasPosition)
                .OrderBy(a => a.OnGround ? 1 : 0)               // Airborne first
                .ThenByDescending(a => a.BaroAltitude ?? 0)     // Higher altitude first
                .Take(maxCount)
                .ToList();
        }

        private double? TryPropertyDouble(System.Text.Json.JsonElement[] arr, int index)
        {
            if (index >= arr.Length) return null;
            var elem = arr[index];
            if (elem.ValueKind == System.Text.Json.JsonValueKind.Null) return null;
            try { return elem.GetDouble(); } catch { return null; }
        }

        private float? TryPropertyFloat(System.Text.Json.JsonElement[] arr, int index)
        {
            if (index >= arr.Length) return null;
            var elem = arr[index];
            if (elem.ValueKind == System.Text.Json.JsonValueKind.Null) return null;
            try { return (float)elem.GetDouble(); } catch { return null; }
        }
    }
}