using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FattoPrizzerva.BuildTelemetry
{
    internal sealed class BuildTelemetryCollector : MonoBehaviour
    {
        private sealed class RawCounter : IDisposable
        {
            private ProfilerRecorder _recorder;

            public readonly string Field;
            public readonly string RawUnit;
            public readonly int ValidityBit;
            public string Category { get; private set; } = "Unavailable";
            public string ProfilerName { get; private set; } = "Unavailable";
            public bool Available { get; private set; }

            public RawCounter(
                string field,
                string rawUnit,
                int validityBit,
                params (ProfilerCategory category, string categoryName, string profilerName)[] candidates)
            {
                Field = field;
                RawUnit = rawUnit;
                ValidityBit = validityBit;

                foreach ((ProfilerCategory category, string categoryName, string profilerName) in candidates)
                {
                    try
                    {
                        _recorder = ProfilerRecorder.StartNew(category, profilerName, 1);
                        if (_recorder.Valid)
                        {
                            Category = categoryName;
                            ProfilerName = profilerName;
                            Available = true;
                            return;
                        }

                        _recorder.Dispose();
                    }
                    catch (Exception)
                    {
                        // Counter availability differs by graphics API and build type.
                    }
                }
            }

            public long Read(ref int validityFlags)
            {
                if (!Available || !_recorder.Valid)
                    return 0;

                validityFlags |= 1 << ValidityBit;
                return _recorder.LastValue;
            }

            public TelemetryCounterDefinition ToDefinition()
            {
                return new TelemetryCounterDefinition
                {
                    field = Field,
                    category = Category,
                    profilerName = ProfilerName,
                    rawUnit = RawUnit,
                    available = Available,
                    validityBit = ValidityBit
                };
            }

            public void Dispose()
            {
                try
                {
                    _recorder.Dispose();
                }
                catch (Exception)
                {
                    // A recorder that was not available may already be in its default state.
                }
            }
        }

        internal static BuildTelemetryCollector Active { get; private set; }

        private readonly List<RawCounter> _counters = new List<RawCounter>(15);
        private RawTelemetryWriter _writer;
        private RawFrameSample[] _frameBuffer;
        private int _frameBufferCount;
        private bool _stopped;

        private void Awake()
        {
            if (Active != null && Active != this)
            {
                Destroy(gameObject);
                return;
            }

            Active = this;

            try
            {
                ApplyRequestedQualityLevel();
                CreateCounters();

                TelemetrySessionMetadata metadata = CreateMetadata();
                string sessionDirectory = CreateSessionDirectory(metadata);
                _writer = new RawTelemetryWriter(sessionDirectory, metadata);
                _frameBuffer = _writer.RentFrameBuffer();

                SceneManager.sceneLoaded += OnSceneLoaded;
                SceneManager.sceneUnloaded += OnSceneUnloaded;
                SceneManager.activeSceneChanged += OnActiveSceneChanged;
                Application.quitting += OnApplicationQuitting;

                RecordEvent("session_start", metadata.productName, metadata.sessionId, SceneManager.GetActiveScene());
                Debug.Log($"[BuildTelemetry] Recording raw data in: {sessionDirectory}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[BuildTelemetry] Could not start raw telemetry.\n{exception}");
                StopRecording(false);
                Destroy(gameObject);
            }
        }

        private void LateUpdate()
        {
            if (_writer == null || _stopped)
                return;

            int validityFlags = 0;
            Scene activeScene = SceneManager.GetActiveScene();

            RawFrameSample sample = new RawFrameSample
            {
                frameIndex = Time.frameCount,
                realtimeSinceStartupSeconds = Time.realtimeSinceStartupAsDouble,
                unscaledDeltaSeconds = Time.unscaledDeltaTime,
                timeScale = Time.timeScale,

                mainThreadNanoseconds = _counters[0].Read(ref validityFlags),
                renderThreadNanoseconds = _counters[1].Read(ref validityFlags),
                gpuFrameNanoseconds = _counters[2].Read(ref validityFlags),
                gcAllocatedBytes = _counters[3].Read(ref validityFlags),
                systemUsedMemoryBytes = _counters[4].Read(ref validityFlags),
                totalUsedMemoryBytes = _counters[5].Read(ref validityFlags),
                totalReservedMemoryBytes = _counters[6].Read(ref validityFlags),
                gfxUsedMemoryBytes = _counters[7].Read(ref validityFlags),
                textureMemoryBytes = _counters[8].Read(ref validityFlags),
                meshMemoryBytes = _counters[9].Read(ref validityFlags),
                drawCalls = _counters[10].Read(ref validityFlags),
                batches = _counters[11].Read(ref validityFlags),
                setPassCalls = _counters[12].Read(ref validityFlags),
                triangles = _counters[13].Read(ref validityFlags),
                vertices = _counters[14].Read(ref validityFlags),

                gcCollectionCountGen0 = SafeCollectionCount(0),
                gcCollectionCountGen1 = SafeCollectionCount(1),
                gcCollectionCountGen2 = SafeCollectionCount(2),
                sceneBuildIndex = activeScene.IsValid() ? activeScene.buildIndex : -1,
                qualityLevel = QualitySettings.GetQualityLevel(),
                width = Screen.width,
                height = Screen.height,
                targetFrameRate = Application.targetFrameRate,
                vSyncCount = QualitySettings.vSyncCount,
                validityFlags = validityFlags
            };

            _frameBuffer[_frameBufferCount++] = sample;
            if (_frameBufferCount < RawTelemetryWriter.FramesPerBuffer)
                return;

            _writer.EnqueueFrames(_frameBuffer, _frameBufferCount);
            _frameBuffer = _writer.RentFrameBuffer();
            _frameBufferCount = 0;
        }

        internal void RecordMarker(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name) || _writer == null || _stopped)
                return;

            RecordEvent("marker", name.Trim(), value ?? string.Empty, SceneManager.GetActiveScene());
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RecordEvent("scene_loaded", scene.name, mode.ToString(), scene);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            RecordEvent("scene_unloaded", scene.name, string.Empty, scene);
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            RecordEvent("active_scene_changed", previous.path, next.path, next);
        }

        private void RecordEvent(string eventType, string name, string value, Scene scene)
        {
            if (_writer == null || _stopped)
                return;

            TelemetryRawEvent rawEvent = new TelemetryRawEvent
            {
                eventType = eventType,
                name = name ?? string.Empty,
                value = value ?? string.Empty,
                scenePath = scene.IsValid() ? scene.path : string.Empty,
                sceneBuildIndex = scene.IsValid() ? scene.buildIndex : -1,
                frameIndex = Time.frameCount,
                realtimeSinceStartupSeconds = Time.realtimeSinceStartupAsDouble,
                utc = DateTime.UtcNow.ToString("O")
            };
            _writer.EnqueueEvent(JsonUtility.ToJson(rawEvent));
        }

        private void ApplyRequestedQualityLevel()
        {
            string requestedQuality = TelemetryArguments.GetValue("-telemetryQuality");
            if (string.IsNullOrWhiteSpace(requestedQuality))
                return;

            string[] names = QualitySettings.names;
            for (int index = 0; index < names.Length; index++)
            {
                if (!string.Equals(names[index], requestedQuality, StringComparison.OrdinalIgnoreCase))
                    continue;

                QualitySettings.SetQualityLevel(index, true);
                return;
            }

            Debug.LogWarning(
                $"[BuildTelemetry] Quality level '{requestedQuality}' does not exist. " +
                $"Using '{names[QualitySettings.GetQualityLevel()]}' instead.");
        }

        private void CreateCounters()
        {
            _counters.Add(new RawCounter("main_thread_ns", "nanoseconds", 0,
                (ProfilerCategory.Render, "Render", "CPU Main Thread Frame Time"),
                (ProfilerCategory.Internal, "Internal", "Main Thread")));
            _counters.Add(new RawCounter("render_thread_ns", "nanoseconds", 1,
                (ProfilerCategory.Render, "Render", "CPU Render Thread Frame Time"),
                (ProfilerCategory.Internal, "Internal", "Render Thread")));
            _counters.Add(new RawCounter("gpu_frame_ns", "nanoseconds", 2,
                (ProfilerCategory.Render, "Render", "GPU Frame Time")));
            _counters.Add(new RawCounter("gc_allocated_bytes", "bytes", 3,
                (ProfilerCategory.Memory, "Memory", "GC Allocated In Frame")));
            _counters.Add(new RawCounter("system_used_memory_bytes", "bytes", 4,
                (ProfilerCategory.Memory, "Memory", "System Used Memory")));
            _counters.Add(new RawCounter("total_used_memory_bytes", "bytes", 5,
                (ProfilerCategory.Memory, "Memory", "Total Used Memory")));
            _counters.Add(new RawCounter("total_reserved_memory_bytes", "bytes", 6,
                (ProfilerCategory.Memory, "Memory", "Total Reserved Memory")));
            _counters.Add(new RawCounter("gfx_used_memory_bytes", "bytes", 7,
                (ProfilerCategory.Memory, "Memory", "Video Used Memory"),
                (ProfilerCategory.Render, "Render", "Video Memory Bytes"),
                (ProfilerCategory.Memory, "Memory", "Gfx Used Memory")));
            _counters.Add(new RawCounter("texture_memory_bytes", "bytes", 8,
                (ProfilerCategory.Memory, "Memory", "Texture Memory"),
                (ProfilerCategory.Render, "Render", "Used Textures Bytes")));
            _counters.Add(new RawCounter("mesh_memory_bytes", "bytes", 9,
                (ProfilerCategory.Memory, "Memory", "Mesh Memory")));
            _counters.Add(new RawCounter("draw_calls", "count", 10,
                (ProfilerCategory.Render, "Render", "Draw Calls Count")));
            _counters.Add(new RawCounter("batches", "count", 11,
                (ProfilerCategory.Render, "Render", "Batches Count")));
            _counters.Add(new RawCounter("set_pass_calls", "count", 12,
                (ProfilerCategory.Render, "Render", "SetPass Calls Count")));
            _counters.Add(new RawCounter("triangles", "count", 13,
                (ProfilerCategory.Render, "Render", "Triangles Count")));
            _counters.Add(new RawCounter("vertices", "count", 14,
                (ProfilerCategory.Render, "Render", "Vertices Count")));
        }

        private TelemetrySessionMetadata CreateMetadata()
        {
            string qualityName = QualitySettings.names[QualitySettings.GetQualityLevel()];
            string sessionId = Guid.NewGuid().ToString("N");
            RenderPipelineAsset renderPipeline = QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline;

            TelemetrySessionMetadata metadata = new TelemetrySessionMetadata
            {
                sessionId = sessionId,
                utcStarted = DateTime.UtcNow.ToString("O"),
                projectName = Application.productName,
                productName = Application.productName,
                applicationVersion = Application.version,
                buildGuid = Application.buildGUID,
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                operatingSystem = SystemInfo.operatingSystem,
                deviceModel = SystemInfo.deviceModel,
                processorType = SystemInfo.processorType,
                processorCount = SystemInfo.processorCount,
                systemMemoryMb = SystemInfo.systemMemorySize,
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                graphicsDeviceVendor = SystemInfo.graphicsDeviceVendor,
                graphicsDeviceVersion = SystemInfo.graphicsDeviceVersion,
                graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                graphicsMemoryMb = SystemInfo.graphicsMemorySize,
                qualityName = qualityName,
                qualityLevel = QualitySettings.GetQualityLevel(),
                width = Screen.width,
                height = Screen.height,
                fullScreenMode = Screen.fullScreenMode.ToString(),
                colorSpace = QualitySettings.activeColorSpace.ToString(),
                renderPipeline = renderPipeline != null ? renderPipeline.GetType().FullName : "Built-in Render Pipeline",
                targetFrameRate = Application.targetFrameRate,
                vSyncCount = QualitySettings.vSyncCount,
                gitCommit = TelemetryArguments.GetValue("-telemetryCommit", "unknown"),
                gitBranch = TelemetryArguments.GetValue("-telemetryBranch", "unknown"),
                gitDirty = string.Equals(
                    TelemetryArguments.GetValue("-telemetryGitDirty", "false"),
                    "true",
                    StringComparison.OrdinalIgnoreCase),
                commandLine = string.Join(" ", Environment.GetCommandLineArgs())
            };

            foreach (RawCounter counter in _counters)
                metadata.counters.Add(counter.ToDefinition());

            return metadata;
        }

        private static string CreateSessionDirectory(TelemetrySessionMetadata metadata)
        {
            string outputRoot = TelemetryArguments.GetValue("-telemetryOutput");
            if (string.IsNullOrWhiteSpace(outputRoot))
                outputRoot = Path.Combine(Application.persistentDataPath, "BuildTelemetryRaw");

            outputRoot = Path.GetFullPath(outputRoot);
            string commit = SafePathPart(metadata.gitCommit, 10);
            string quality = SafePathPart(metadata.qualityName, 24);
            string id = metadata.sessionId.Substring(0, 8);
            string folderName = $"session_{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}_{commit}_{quality}_{id}";
            return Path.Combine(outputRoot, folderName);
        }

        private static string SafePathPart(string value, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            StringBuilder builder = new StringBuilder(value.Length);
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char character in value)
            {
                builder.Append(Array.IndexOf(invalid, character) >= 0 || char.IsWhiteSpace(character)
                    ? '_'
                    : character);
                if (builder.Length >= maximumLength)
                    break;
            }

            return builder.Length == 0 ? "unknown" : builder.ToString();
        }

        private static int SafeCollectionCount(int generation)
        {
            try
            {
                return GC.CollectionCount(generation);
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private void OnApplicationQuitting()
        {
            StopRecording(true);
        }

        private void OnDestroy()
        {
            StopRecording(false);
        }

        private void StopRecording(bool cleanShutdown)
        {
            if (_stopped)
                return;

            _stopped = true;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            Application.quitting -= OnApplicationQuitting;

            if (_writer != null)
            {
                TelemetryRawEvent endEvent = new TelemetryRawEvent
                {
                    eventType = "session_end",
                    name = cleanShutdown ? "clean" : "interrupted",
                    value = string.Empty,
                    scenePath = SceneManager.GetActiveScene().path,
                    sceneBuildIndex = SceneManager.GetActiveScene().buildIndex,
                    frameIndex = Time.frameCount,
                    realtimeSinceStartupSeconds = Time.realtimeSinceStartupAsDouble,
                    utc = DateTime.UtcNow.ToString("O")
                };
                _writer.EnqueueEvent(JsonUtility.ToJson(endEvent));

                if (_frameBuffer != null && _frameBufferCount > 0)
                    _writer.EnqueueFrames(_frameBuffer, _frameBufferCount);
                else
                    _writer.ReturnUnusedBuffer(_frameBuffer);

                _frameBuffer = null;
                _frameBufferCount = 0;
                _writer.Dispose();

                TelemetrySessionEnd sessionEnd = new TelemetrySessionEnd
                {
                    utcEnded = DateTime.UtcNow.ToString("O"),
                    cleanShutdown = cleanShutdown,
                    framesWritten = _writer.FramesWritten,
                    droppedFrameSamples = _writer.DroppedFrameSamples,
                    droppedEvents = _writer.DroppedEvents,
                    writerError = _writer.WriterError ?? string.Empty
                };

                try
                {
                    File.WriteAllText(
                        Path.Combine(_writer.SessionDirectory, "session_end.json"),
                        JsonUtility.ToJson(sessionEnd, true),
                        new UTF8Encoding(false));

                    if (cleanShutdown && string.IsNullOrEmpty(sessionEnd.writerError))
                        File.WriteAllText(Path.Combine(_writer.SessionDirectory, "complete.flag"), "complete\n");
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[BuildTelemetry] Could not write session end metadata.\n{exception}");
                }
            }

            foreach (RawCounter counter in _counters)
                counter.Dispose();
            _counters.Clear();

            if (Active == this)
                Active = null;
        }
    }
}
