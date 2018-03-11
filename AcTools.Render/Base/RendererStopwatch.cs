namespace AcTools.Render.Base {
    public class RendererStopwatch {
        private double _totalTime;
        private long _frame;

        private readonly RendererClock _clock;

        internal RendererStopwatch(RendererClock clock) {
            _clock = clock;
            Reset();
        }

        public double ElapsedSeconds {
            get {
                _clock.Update(ref _totalTime, ref _frame, IsPaused);
                return _totalTime;
            }
        }

        public bool IsPaused { get; set; }

        public void Pause() {
            IsPaused = true;
        }

        public void Play() {
            IsPaused = false;
        }

        public void Reset() {
            _totalTime = _clock.GetLastFrameTime();
            _frame = _clock.GetFrame();
        }
    }
}