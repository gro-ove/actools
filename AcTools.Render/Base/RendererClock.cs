using System;

namespace AcTools.Render.Base {
    public class RendererClock {
        private readonly float[] _previousFrames;

        public RendererClock(int bufferSize) {
            _previousFrames = new float[bufferSize];
        }

        private double _totalTimeInBuffer;
        private int _previousFrameId = -1;
        private long _totalFrames;

        public void RegisterFrame(float dt) {
            _totalFrames++;
            if (++_previousFrameId >= _previousFrames.Length) {
                _previousFrameId = 0;
            }

            _totalTimeInBuffer += dt - _previousFrames[_previousFrameId];
            _previousFrames[_previousFrameId] = dt;
        }

        public double GetTotalTime(long frames) {
            if (frames == 0 || _previousFrameId < 0) return 0d;
            if (frames == 1) return _previousFrames[_previousFrameId];

            if (frames >= _previousFrames.Length || frames >= _totalFrames) {
                return _totalTimeInBuffer / Math.Min(_previousFrames.Length, _totalFrames) * frames;
            }

            var result = 0d;
            var anotherEnd = frames - _previousFrameId - 1;

            for (var i = Math.Max(-anotherEnd, 0); i <= _previousFrameId; i++) {
                result += _previousFrames[i];
            }

            for (var i = _previousFrames.Length - anotherEnd; i < _previousFrames.Length; i++) {
                result += _previousFrames[i];
            }

            return result;
        }

        internal void Update(ref double totalTime, ref long frame, bool pause) {
            if (!pause) {
                if (frame == -1 || _totalFrames == 0) {
                    totalTime = 0d;
                } else {
                    var difference = _totalFrames - frame;
                    if (difference <= 0) return;
                    totalTime += GetTotalTime(difference);
                }
            }

            frame = _totalFrames;
        }

        internal long GetFrame() => _totalFrames;

        public double GetLastFrameTime() {
            return _previousFrameId == -1 ? 0d : _previousFrames[_previousFrameId];
        }
    }
}