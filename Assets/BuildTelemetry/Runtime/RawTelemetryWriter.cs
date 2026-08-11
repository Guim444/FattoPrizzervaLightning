using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace FattoPrizzerva.BuildTelemetry
{
    internal sealed class RawTelemetryWriter : IDisposable
    {
        public const int FramesPerBuffer = 256;
        public const int BinaryFormatVersion = 1;
        public const int BinaryRecordSize = 184;

        private abstract class WriteCommand
        {
        }

        private sealed class FrameCommand : WriteCommand
        {
            public RawFrameSample[] samples;
            public int count;
        }

        private sealed class EventCommand : WriteCommand
        {
            public string jsonLine;
        }

        private readonly BlockingCollection<WriteCommand> _queue =
            new BlockingCollection<WriteCommand>(32);
        private readonly ConcurrentBag<RawFrameSample[]> _bufferPool =
            new ConcurrentBag<RawFrameSample[]>();
        private readonly Thread _thread;
        private int _disposed;
        private long _framesWritten;
        private long _droppedFrameSamples;
        private long _droppedEvents;
        private string _writerError = string.Empty;

        public string SessionDirectory { get; }
        public long FramesWritten => Interlocked.Read(ref _framesWritten);
        public long DroppedFrameSamples => Interlocked.Read(ref _droppedFrameSamples);
        public long DroppedEvents => Interlocked.Read(ref _droppedEvents);
        public string WriterError => _writerError;

        public RawTelemetryWriter(string sessionDirectory, TelemetrySessionMetadata metadata)
        {
            SessionDirectory = sessionDirectory;
            Directory.CreateDirectory(SessionDirectory);

            string metadataPath = Path.Combine(SessionDirectory, "session.json");
            File.WriteAllText(
                metadataPath,
                JsonUtility.ToJson(metadata, true),
                new UTF8Encoding(false));

            _thread = new Thread(WriterLoop)
            {
                IsBackground = true,
                Name = "FattoPrizzerva Raw Telemetry Writer",
                Priority = System.Threading.ThreadPriority.BelowNormal
            };
            _thread.Start();
        }

        public RawFrameSample[] RentFrameBuffer()
        {
            return _bufferPool.TryTake(out RawFrameSample[] buffer)
                ? buffer
                : new RawFrameSample[FramesPerBuffer];
        }

        public void ReturnUnusedBuffer(RawFrameSample[] buffer)
        {
            if (buffer != null && buffer.Length == FramesPerBuffer)
                _bufferPool.Add(buffer);
        }

        public void EnqueueFrames(RawFrameSample[] samples, int count)
        {
            if (samples == null || count <= 0)
            {
                ReturnUnusedBuffer(samples);
                return;
            }

            FrameCommand command = new FrameCommand { samples = samples, count = count };
            if (Volatile.Read(ref _disposed) != 0 || !_queue.TryAdd(command))
            {
                Interlocked.Add(ref _droppedFrameSamples, count);
                ReturnUnusedBuffer(samples);
            }
        }

        public void EnqueueEvent(string jsonLine)
        {
            if (string.IsNullOrEmpty(jsonLine))
                return;

            EventCommand command = new EventCommand { jsonLine = jsonLine };
            if (Volatile.Read(ref _disposed) != 0 || !_queue.TryAdd(command))
                Interlocked.Increment(ref _droppedEvents);
        }

        private void WriterLoop()
        {
            string framesPath = Path.Combine(SessionDirectory, "frames.bin");
            string eventsPath = Path.Combine(SessionDirectory, "events.jsonl");

            try
            {
                using FileStream frameStream = new FileStream(
                    framesPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read,
                    1024 * 1024,
                    FileOptions.SequentialScan);
                using BinaryWriter frameWriter = new BinaryWriter(frameStream, Encoding.UTF8, false);
                using StreamWriter eventWriter = new StreamWriter(
                    new FileStream(eventsPath, FileMode.Create, FileAccess.Write, FileShare.Read),
                    new UTF8Encoding(false));

                frameWriter.Write(new[] { (byte)'F', (byte)'B', (byte)'T', (byte)'L' });
                frameWriter.Write(BinaryFormatVersion);
                frameWriter.Write(BinaryRecordSize);

                foreach (WriteCommand command in _queue.GetConsumingEnumerable())
                {
                    if (command is FrameCommand frameCommand)
                    {
                        WriteFrames(frameWriter, frameCommand.samples, frameCommand.count);
                        Interlocked.Add(ref _framesWritten, frameCommand.count);
                        ReturnUnusedBuffer(frameCommand.samples);
                    }
                    else if (command is EventCommand eventCommand)
                    {
                        eventWriter.WriteLine(eventCommand.jsonLine);
                    }
                }

                frameWriter.Flush();
                eventWriter.Flush();
            }
            catch (Exception exception)
            {
                _writerError = exception.ToString();
            }
        }

        private static void WriteFrames(BinaryWriter writer, IReadOnlyList<RawFrameSample> samples, int count)
        {
            for (int index = 0; index < count; index++)
            {
                RawFrameSample sample = samples[index];
                writer.Write(sample.frameIndex);
                writer.Write(sample.realtimeSinceStartupSeconds);
                writer.Write(sample.unscaledDeltaSeconds);
                writer.Write(sample.timeScale);

                writer.Write(sample.mainThreadNanoseconds);
                writer.Write(sample.renderThreadNanoseconds);
                writer.Write(sample.gpuFrameNanoseconds);
                writer.Write(sample.gcAllocatedBytes);
                writer.Write(sample.systemUsedMemoryBytes);
                writer.Write(sample.totalUsedMemoryBytes);
                writer.Write(sample.totalReservedMemoryBytes);
                writer.Write(sample.gfxUsedMemoryBytes);
                writer.Write(sample.textureMemoryBytes);
                writer.Write(sample.meshMemoryBytes);
                writer.Write(sample.drawCalls);
                writer.Write(sample.batches);
                writer.Write(sample.setPassCalls);
                writer.Write(sample.triangles);
                writer.Write(sample.vertices);

                writer.Write(sample.gcCollectionCountGen0);
                writer.Write(sample.gcCollectionCountGen1);
                writer.Write(sample.gcCollectionCountGen2);
                writer.Write(sample.sceneBuildIndex);
                writer.Write(sample.qualityLevel);
                writer.Write(sample.width);
                writer.Write(sample.height);
                writer.Write(sample.targetFrameRate);
                writer.Write(sample.vSyncCount);
                writer.Write(sample.validityFlags);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _queue.CompleteAdding();
            if (!_thread.Join(TimeSpan.FromSeconds(15)))
            {
                _writerError = "The telemetry writer did not finish within 15 seconds.";
                return;
            }

            _queue.Dispose();
        }
    }
}
