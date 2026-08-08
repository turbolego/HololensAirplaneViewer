using System.Collections.Generic;
using System.Linq;
using HololensAirplaneViewer.Models;

namespace HololensAirplaneViewer.Services
{
    /// <summary>
    /// Pure aircraft-selection logic: given parsed aircraft states, return the
    /// subset that should be rendered. No UWP dependencies — unit-testable in
    /// a plain .NET test project.
    /// </summary>
    public static class AirplaneSelection
    {
        /// <summary>Max on-ground aircraft kept from the observer's immediate area.</summary>
        public const int MaxNearbyGroundAircraft = 5;

        /// <summary>
        /// Returns up to maxCount aircraft that currently have valid GPS positions.
        /// Ranking (matching what a viewer at the observer position cares about):
        ///   1. on-ground traffic within <paramref name="includeGroundWithinKm"/>
        ///      of the observer (closest first, capped at
        ///      <see cref="MaxNearbyGroundAircraft"/>) — e.g. aircraft at your
        ///      airport, which busy airborne traffic would otherwise starve out;
        ///   2. airborne aircraft (highest altitude first);
        ///   3. far-away on-ground aircraft.
        /// </summary>
        public static List<AirplaneState> Select(
            IEnumerable<AirplaneState> states, int maxCount,
            double? observerLatDeg = null, double? observerLonDeg = null,
            double includeGroundWithinKm = 0.0)
        {
            bool hasObserver = observerLatDeg.HasValue && observerLonDeg.HasValue;

            double DistanceKm(AirplaneState a) => AirplaneMath.GreatCircleDistanceMeters(
                observerLatDeg.Value, observerLonDeg.Value,
                a.Latitude.Value, a.Longitude.Value) / 1000.0;

            bool IsNearbyGround(AirplaneState a) =>
                a.OnGround && hasObserver && includeGroundWithinKm > 0.0
                && DistanceKm(a) <= includeGroundWithinKm;

            var withPosition = states.Where(a => a.HasPosition).ToList();

            var nearbyGround = withPosition
                .Where(IsNearbyGround)
                .OrderBy(DistanceKm)
                .Take(MaxNearbyGroundAircraft);

            var airborne = withPosition
                .Where(a => !a.OnGround)
                .OrderByDescending(a => a.BaroAltitude ?? 0);

            var farGround = withPosition
                .Where(a => a.OnGround && !IsNearbyGround(a))
                .OrderByDescending(a => a.BaroAltitude ?? 0);

            return nearbyGround.Concat(airborne).Concat(farGround)
                .Take(maxCount)
                .ToList();
        }

        /// <summary>
        /// Filters a list down to aircraft that are physically visible: they have
        /// a position AND are above the Earth's horizon from the observer's GPS
        /// fix (line of sight). Keeps relative ordering.
        /// </summary>
        public static List<AirplaneState> OnlyVisibleFrom(
            IEnumerable<AirplaneState> states,
            double observerLatDeg, double observerLonDeg, double observerAltM)
        {
            return states
                .Where(a => a.HasPosition
                    && AirplaneMath.IsAboveHorizon(
                        observerLatDeg, observerLonDeg, observerAltM,
                        a.Latitude.Value, a.Longitude.Value, a.AltMeters))
                .ToList();
        }
    }
}
