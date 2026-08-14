namespace FattoPrizzerva.BuildTelemetry
{
    /// <summary>
    /// Optional raw markers for later correlation with frame data.
    /// Call from Unity's main thread. No statistics are calculated here.
    /// </summary>
    public static class BuildTelemetryApi
    {
        public static bool IsRecording => BuildTelemetryCollector.Active != null;

        public static void Mark(string name, string value = "")
        {
            BuildTelemetryCollector.Active?.RecordMarker(name, value);
        }
    }
}
