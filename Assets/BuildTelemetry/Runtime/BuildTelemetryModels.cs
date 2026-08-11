using System;
using System.Collections.Generic;

namespace FattoPrizzerva.BuildTelemetry
{
    [Serializable]
    internal sealed class TelemetryCounterDefinition
    {
        public string field;
        public string category;
        public string profilerName;
        public string rawUnit;
        public bool available;
        public int validityBit;
    }

    [Serializable]
    internal sealed class TelemetrySessionMetadata
    {
        public int schemaVersion = 1;
        public string frameFormat = "frames.bin / FBTL v1";
        public string sessionId;
        public string utcStarted;
        public string projectName;
        public string productName;
        public string applicationVersion;
        public string buildGuid;
        public string unityVersion;
        public string platform;
        public string operatingSystem;
        public string deviceModel;
        public string processorType;
        public int processorCount;
        public int systemMemoryMb;
        public string graphicsDeviceName;
        public string graphicsDeviceVendor;
        public string graphicsDeviceVersion;
        public string graphicsDeviceType;
        public int graphicsMemoryMb;
        public string qualityName;
        public int qualityLevel;
        public int width;
        public int height;
        public string fullScreenMode;
        public string colorSpace;
        public string renderPipeline;
        public int targetFrameRate;
        public int vSyncCount;
        public string gitCommit;
        public string gitBranch;
        public bool gitDirty;
        public string commandLine;
        public List<TelemetryCounterDefinition> counters = new List<TelemetryCounterDefinition>();
    }

    [Serializable]
    internal sealed class TelemetryRawEvent
    {
        public string eventType;
        public string name;
        public string value;
        public string scenePath;
        public int sceneBuildIndex;
        public int frameIndex;
        public double realtimeSinceStartupSeconds;
        public string utc;
    }

    [Serializable]
    internal sealed class TelemetrySessionEnd
    {
        public int schemaVersion = 1;
        public string utcEnded;
        public bool cleanShutdown;
        public long framesWritten;
        public long droppedFrameSamples;
        public long droppedEvents;
        public string writerError;
    }

    internal struct RawFrameSample
    {
        public long frameIndex;
        public double realtimeSinceStartupSeconds;
        public float unscaledDeltaSeconds;
        public float timeScale;

        public long mainThreadNanoseconds;
        public long renderThreadNanoseconds;
        public long gpuFrameNanoseconds;
        public long gcAllocatedBytes;
        public long systemUsedMemoryBytes;
        public long totalUsedMemoryBytes;
        public long totalReservedMemoryBytes;
        public long gfxUsedMemoryBytes;
        public long textureMemoryBytes;
        public long meshMemoryBytes;
        public long drawCalls;
        public long batches;
        public long setPassCalls;
        public long triangles;
        public long vertices;

        public int gcCollectionCountGen0;
        public int gcCollectionCountGen1;
        public int gcCollectionCountGen2;
        public int sceneBuildIndex;
        public int qualityLevel;
        public int width;
        public int height;
        public int targetFrameRate;
        public int vSyncCount;
        public int validityFlags;
    }

    internal static class TelemetryArguments
    {
        public static bool HasFlag(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static string GetValue(string name, string fallback = "")
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            }

            return fallback;
        }
    }
}
