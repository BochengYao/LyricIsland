using System;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LyricHover.App.Fullscreen
{
    internal sealed class AudioEnergySnapshot
    {
        public double Bass { get; set; }
        public double Loudness { get; set; }
        public double Transient { get; set; }
        public bool IsAvailable { get; set; }
    }

    /// <summary>
    /// Reads the default Windows render endpoint through WASAPI loopback. Samples are
    /// reduced to energy values in memory and are never stored or transmitted.
    /// </summary>
    internal sealed class SystemOutputAudioAnalyzer : IDisposable
    {
        private readonly object sync = new object();
        private WasapiLoopbackCapture capture;
        private AudioEnergySnapshot current = new AudioEnergySnapshot();
        private float lowPass;
        private double envelope;
        private bool disposed;

        public AudioEnergySnapshot Current
        {
            get { lock (sync) return new AudioEnergySnapshot { Bass = current.Bass, Loudness = current.Loudness, Transient = current.Transient, IsAvailable = current.IsAvailable }; }
        }

        public bool TryStart()
        {
            if (disposed) return false;
            StopCapture();
            try
            {
                using (var enumerator = new MMDeviceEnumerator())
                {
                    var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    capture = new WasapiLoopbackCapture(device);
                }
                capture.DataAvailable += OnDataAvailable;
                capture.RecordingStopped += OnRecordingStopped;
                capture.StartRecording();
                lock (sync) current.IsAvailable = true;
                return true;
            }
            catch
            {
                StopCapture();
                return false;
            }
        }

        public void Stop()
        {
            if (!disposed) StopCapture();
        }

        private void OnDataAvailable(object sender, WaveInEventArgs args)
        {
            var waveFormat = capture?.WaveFormat;
            if (waveFormat == null || args.BytesRecorded <= 0) return;
            var bytesPerSample = waveFormat.BitsPerSample / 8;
            if (bytesPerSample != 4 && bytesPerSample != 2) return;
            var step = Math.Max(bytesPerSample, bytesPerSample * waveFormat.Channels);
            double sum = 0;
            double bassSum = 0;
            var count = 0;
            for (var offset = 0; offset + bytesPerSample <= args.BytesRecorded; offset += step)
            {
                float sample = bytesPerSample == 4
                    ? BitConverter.ToSingle(args.Buffer, offset)
                    : BitConverter.ToInt16(args.Buffer, offset) / 32768f;
                if (float.IsNaN(sample) || float.IsInfinity(sample)) continue;
                sample = Math.Max(-1f, Math.Min(1f, sample));
                lowPass += 0.035f * (sample - lowPass);
                sum += sample * sample;
                bassSum += lowPass * lowPass;
                count++;
            }
            if (count == 0) return;
            var loudness = Math.Min(1, Math.Sqrt(sum / count) * 3.1);
            var bass = Math.Min(1, Math.Sqrt(bassSum / count) * 5.2);
            var attack = loudness > envelope ? 0.65 : 0.10;
            var previous = envelope;
            envelope += (loudness - envelope) * attack;
            var transient = Math.Max(0, Math.Min(1, (loudness - previous - 0.035) * 5.5));
            lock (sync)
            {
                current.Bass = Math.Max(current.Bass * 0.82, bass);
                current.Loudness = envelope;
                current.Transient = Math.Max(current.Transient * 0.70, transient);
                current.IsAvailable = true;
            }
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs args)
        {
            lock (sync) current.IsAvailable = false;
        }

        private void StopCapture()
        {
            var value = Interlocked.Exchange(ref capture, null);
            if (value == null) return;
            try { value.DataAvailable -= OnDataAvailable; value.RecordingStopped -= OnRecordingStopped; value.StopRecording(); }
            catch { }
            value.Dispose();
            lock (sync) current = new AudioEnergySnapshot();
        }

        public void Dispose()
        {
            disposed = true;
            StopCapture();
        }
    }
}
