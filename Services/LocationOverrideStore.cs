using System;

namespace HololensAirplaneViewer.Services
{
    /// <summary>
    /// Thread-safe bridge between the native XAML settings view and the
    /// Direct3D holographic view. The override remains active until replaced
    /// by another manual location.
    /// </summary>
    public static class LocationOverrideStore
    {
        private static readonly object SyncRoot = new object();
        private static bool hasOverride;
        private static double latitude;
        private static double longitude;
        private static int generation;

        public static void Set(double newLatitude, double newLongitude)
        {
            lock (SyncRoot)
            {
                latitude = newLatitude;
                longitude = newLongitude;
                hasOverride = true;
                generation++;
            }
        }

        /// <summary>
        /// Drops the manual override so the app returns to the automatic
        /// (device supplied) location on the next update.
        /// </summary>
        public static void Clear()
        {
            lock (SyncRoot)
            {
                if (!hasOverride)
                {
                    return;
                }

                hasOverride = false;
                // Reset coordinates to a neutral state. This prevents the renderer
                // from briefly showing stale manual coordinates (or 0,0) after a
                // clear operation while still signalling a generation change.
                latitude = 0.0;
                longitude = 0.0;
                // Increment generation to trigger an immediate retry in the
                // renderer when it detects the mismatch.
                generation++;
            }
        }

        public static bool TryGet(out double currentLatitude, out double currentLongitude)
        {
            lock (SyncRoot)
            {
                currentLatitude = latitude;
                currentLongitude = longitude;
                return hasOverride;
            }
        }

        /// <summary>
        /// Returns the current generation counter value. Each call to Set()
        /// increments this counter. Used by AirplaneRenderer to detect
        /// stale in-flight fetches.
        /// </summary>
        public static int GetGeneration()
        {
            lock (SyncRoot)
            {
                return generation;
            }
        }
    }
}
