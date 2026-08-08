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
        /// <summary>
        /// Returns up to maxCount aircraft that currently have valid GPS positions,
        /// sorted airborne first, then by highest altitude.
        /// </summary>
        public static List<AirplaneState> Select(
            IEnumerable<AirplaneState> states, int maxCount)
        {
            return states
                .Where(a => a.HasPosition)
                .OrderBy(a => a.OnGround ? 1 : 0)
                .ThenByDescending(a => a.BaroAltitude ?? 0)
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
