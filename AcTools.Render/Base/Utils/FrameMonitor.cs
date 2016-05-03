using System.Diagnostics;

namespace AcTools.Render.Base.Utils {
    public class FrameMonitor {
        private float _sampleInterval = 0.25f;
        private int _totalFrames;
        private float _totalTime = 1f;
        private int _frames;
        private readonly Stopwatch _timing = new Stopwatch();

        public FrameMonitor() {
            _timing.Reset();
            _timing.Start();
        }

        public void Tick() {
            _frames++;
            if (!(_timing.Elapsed.TotalSeconds >= _sampleInterval)) return;

            _totalTime = (float)_timing.Elapsed.TotalSeconds;
            _totalFrames = _frames;
            _frames = 0;
            _timing.Restart();

            FramesPerSecond += (_totalFrames / _totalTime - FramesPerSecond) * 0.7f;
        }

        public float FramesPerSecond { get; private set; }
    }
}