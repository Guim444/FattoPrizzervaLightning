using UnityEngine;

namespace FattoPrizzerva.BuildTelemetry
{
    internal static class BuildTelemetryBootstrap
    {
#if BUILD_TELEMETRY
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateCollector()
        {
            if (!TelemetryArguments.HasFlag("-buildTelemetry") || BuildTelemetryCollector.Active != null)
                return;

            GameObject telemetryObject = new GameObject("[Build Telemetry]");
            Object.DontDestroyOnLoad(telemetryObject);
            telemetryObject.AddComponent<BuildTelemetryCollector>();
        }
#endif
    }
}
