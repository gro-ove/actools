using System.Diagnostics;

namespace AcTools.Render.Base.Utils {
    public class FrameMonitor {
        private float _sampleInterval = 0.5f;
        private int _totalFrames;
        private float _totalTime = 1f;
        private int _frames;
        private readonly Stopwatch _timing = new Stopwatch();
        private float _framesPerSecond;

        public FrameMonitor() {
            _timing.Reset();
            _timing.Start();
        }

        private bool Update() {
            if (!(_timing.Elapsed.TotalSeconds >= _sampleInterval)) return false;

            _totalTime = (float)_timing.Elapsed.TotalSeconds;
            _totalFrames = _frames;
            _frames = 0;
            _timing.Restart();

            _framesPerSecond = _totalFrames / _totalTime;
            return true;
        }

        public bool Tick() {
            _frames++;
            return Update();
        }

        public float FramesPerSecond {
            get {
                Update();
                return _framesPerSecond;
            }
        }
    }
}