using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using HololensAirplaneViewer.Models;
using HololensAirplaneViewer.Services;

namespace VerifyAirplanes
{
    /// <summary>
    /// End-to-end verification of the airplane app pipeline against LIVE
    /// OpenSky Network data — exactly like the HoloLens would.
    ///
    /// Replicates AirplaneService.GetLiveStatesAsync (bbox + parse) and
    /// AirplaneRenderer.Update (Select + OnlyVisibleFrom), then reports which
    /// aircraft would actually be rendered in the dome, and checks whether a
    /// target callsign (default SAS1314) makes it through the full pipeline.
    ///
    /// Usage:
    ///   dotnet run                                   (Gardermoen 60.197552, 11.100415, check SAS1314)
    ///   dotnet run -- &lt;lat&gt; &lt;lon&gt;                     (any observer GPS fix)
    ///   dotnet run -- &lt;lat&gt; &lt;lon&gt; &lt;callsign&gt;          (also probe a specific flight)
    /// </summary>
    internal static class Program
    {
        // Gardermoen (ENGM), Norway — default observer GPS fix
        private const double DefaultLat = 60.197552;
        private const double DefaultLon = 11.100415;
        private const double BoxHalfSpanDegrees = 3.0;
        private const int MaxAirplanesRendered = 15;
        private const double GroundTrafficRadiusKm = 15.0;

        private static double lat;
        private static double lon;

        private static async Task<int> Main(string[] args)
        {
            lat = DefaultLat;
            lon = DefaultLon;
            string probe = "SAS1314";

            if (args.Length >= 1 && double.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a))
            {
                lat = a;
            }
            if (args.Length >= 2 && double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
            {
                lon = b;
            }
            if (args.Length >= 3 && !string.IsNullOrWhiteSpace(args[2]))
            {
                probe = args[2].Trim().ToUpperInvariant();
            }

            var url = string.Format(
                CultureInfo.InvariantCulture,
                "https://opensky-network.org/api/states/all?lamin={0}&lomin={1}&lamax={2}&lomax={3}",
                lat - BoxHalfSpanDegrees, lon - BoxHalfSpanDegrees,
                lat + BoxHalfSpanDegrees, lon + BoxHalfSpanDegrees);

            Console.WriteLine($"Observer GPS: {lat:F6}, {lon:F6}");
            Console.WriteLine($"Query box (lat/lon ±{BoxHalfSpanDegrees:F0}°): " +
                              $"[{lat - BoxHalfSpanDegrees:F2}, {lat + BoxHalfSpanDegrees:F2}] / " +
                              $"[{lon - BoxHalfSpanDegrees:F2}, {lon + BoxHalfSpanDegrees:F2}]");
            Console.WriteLine();

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            using var resp = await http.GetAsync(url);
            Console.WriteLine($"HTTP {(int)resp.StatusCode} {resp.StatusCode}");
            var raw = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"Response length: {raw.Length}");
            if (raw.Length < 200)
            {
                Console.WriteLine($"RAW: {raw}");
                return 1;
            }

            var parsed = ParseStates(raw);
            Console.WriteLine($"Parsed aircraft in box: {parsed.Count}");

            // What AirplaneService.GetLiveStatesAsync returns (Select inside the
            // service) — airborne first, plus on-ground traffic within 15 km.
            var selected = AirplaneSelection.Select(
                parsed, MaxAirplanesRendered,
                lat, lon, GroundTrafficRadiusKm);
            Console.WriteLine($"After Select(max {MaxAirplanesRendered}, ground<{GroundTrafficRadiusKm}km): {selected.Count}");

            // What AirplaneRenderer.Update renders (line of sight from the fix)
            var visible = AirplaneSelection.OnlyVisibleFrom(
                selected, lat, lon,
                AirplaneMath.DefaultObserverAltitudeMeters);
            Console.WriteLine($"After line-of-sight filter: {visible.Count} rendered in dome");
            Console.WriteLine();

            Console.WriteLine($"{"Call",-10} {"Alt(m)",-8} {"Dist(km)",-10} {"Horiz(km)",-10} {"LOS",-6} {"DomeX",-7} {"DomeY",-7} {"DomeZ",-7}");
            Console.WriteLine(new string('-', 80));

            foreach (var p in visible)
            {
                var dome = AirplaneMath.ComputeDomePosition(
                    p.Latitude.Value, p.Longitude.Value, p.AltMeters,
                    lat, lon,
                    Vector3.Zero, 1.4f);
                double distKm = AirplaneMath.GreatCircleDistanceMeters(
                    lat, lon, p.Latitude.Value, p.Longitude.Value) / 1000.0;
                double horizKm = AirplaneMath.HorizonDistanceMeters(
                    AirplaneMath.DefaultObserverAltitudeMeters, p.AltMeters) / 1000.0;

                Console.WriteLine(
                    $"{p.DisplayName,-10} {p.AltMeters,8:F0} {distKm,10:F1} {horizKm,10:F1} {IsLos(p),-6} {dome.X,7:F2} {dome.Y,7:F2} {dome.Z,7:F2}");
            }

            Console.WriteLine();

            // Probe the target flight through the FULL app pipeline (Select over
            // all parsed states, then line-of-sight) so slot competition with
            // airborne aircraft is real, not an isolated check.
            CheckFor(parsed, probe, selected, visible);
            return 0;
        }

        private static bool IsLos(AirplaneState p) =>
            AirplaneMath.IsAboveHorizon(
                lat, lon, AirplaneMath.DefaultObserverAltitudeMeters,
                p.Latitude.Value, p.Longitude.Value, p.AltMeters);

        private static void CheckFor(
            List<AirplaneState> parsed, string needle,
            List<AirplaneState> selected, List<AirplaneState> visible)
        {
            var matches = parsed
                .Where(p => (p.Callsign ?? "").ToUpperInvariant().Contains(needle)
                         || (p.Icao24 ?? "").ToUpperInvariant().Contains(needle))
                .ToList();

            if (matches.Count == 0)
            {
                Console.WriteLine($"'{needle}': NOT found in the ±{BoxHalfSpanDegrees:F0}° box right now.");
                return;
            }

            foreach (var p in matches)
            {
                bool inSelected = selected.Contains(p);
                bool inVisible = visible.Contains(p);
                double distKm = AirplaneMath.GreatCircleDistanceMeters(
                    lat, lon, p.Latitude.Value, p.Longitude.Value) / 1000.0;
                int selectedRank = selected.IndexOf(p);

                Console.WriteLine(
                    $"'{needle}' -> {p.DisplayName} ({p.Icao24}, {p.OriginCountry}) " +
                    $"lat {p.Latitude.Value:F5} lon {p.Longitude.Value:F5} " +
                    $"alt {p.AltMeters:F0}m onGround={p.OnGround} dist={distKm:F1}km " +
                    $"| in Select(top{MaxAirplanesRendered}): {inSelected}{(inSelected ? $" (rank {selectedRank + 1})" : " (dropped: higher-ranked aircraft filled all slots)")} " +
                    $"| rendered in dome: {inVisible}");
            }
        }

        // Replicates AirplaneService.ParseStatesIntoList — mirrors the app's
        // OpenSky state-vector contract (17 fields, see AirplaneMath).
        private static List<AirplaneState> ParseStates(string json)
        {
            var list = new List<AirplaneState>();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("states", out var states))
                return list;

            foreach (var entry in states.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Array)
                    continue;
                var arr = entry.EnumerateArray().ToArray();
                if (arr.Length < AirplaneMath.OpenSkyStateVectorMinimumFields)
                    continue;

                double? lon = GetDouble(arr, 5);
                double? lat = GetDouble(arr, 6);
                if (!lon.HasValue || !lat.HasValue)
                    continue;

                list.Add(new AirplaneState
                {
                    Icao24 = GetString(arr, 0) ?? "?",
                    Callsign = GetString(arr, 1)?.Trim() ?? "",
                    OriginCountry = GetString(arr, 2) ?? "",
                    Longitude = lon,
                    Latitude = lat,
                    BaroAltitude = GetFloat(arr, 7),
                    OnGround = arr[8].ValueKind != JsonValueKind.Null && arr[8].GetBoolean(),
                    Velocity = GetFloat(arr, 9),
                    TrueTrack = GetFloat(arr, 10),
                    GeoAltitude = GetFloat(arr, 13),
                });
            }
            return list;
        }

        private static string GetString(JsonElement[] arr, int idx)
        {
            if (idx >= arr.Length || arr[idx].ValueKind == JsonValueKind.Null)
                return null;
            return arr[idx].GetString();
        }

        private static double? GetDouble(JsonElement[] arr, int idx)
        {
            if (idx >= arr.Length || arr[idx].ValueKind == JsonValueKind.Null)
                return null;
            return arr[idx].GetDouble();
        }

        private static float? GetFloat(JsonElement[] arr, int idx)
        {
            var d = GetDouble(arr, idx);
            return d.HasValue ? (float)d.Value : (float?)null;
        }
    }
}
