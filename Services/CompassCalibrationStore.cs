
using Windows.Storage;

namespace HololensAirplaneViewer.Services
{
    public static class CompassCalibrationStore
    {
        private const string Key = "northCalibrationOffset";
        private static readonly ApplicationDataContainer LocalSettings = ApplicationData.Current.LocalSettings;

        public static void SaveOffset(float offsetDegrees)
        {
            LocalSettings.Values[Key] = offsetDegrees;
        }

        public static float LoadOffset()
        {
            if (LocalSettings.Values.TryGetValue(Key, out object value) && value is float f)
                return f;
            // Stored as double sometimes due to JSON serialization
            if (value is double d)
                return (float)d;
            return 0f;
        }
    }
}
