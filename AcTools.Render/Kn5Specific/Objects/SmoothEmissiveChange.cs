using System;
using AcTools.Render.Base;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Utils;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Objects {
    public class SmoothEmissiveChange {
        private Vector3? _value;
        private RendererStopwatch _stopwatch;
        private bool _off;
        private bool _smoothChanging;
        private float _position;
        
        private TimeSpan? _duration;

        public SmoothEmissiveChange(TimeSpan? duration = null) {
            _duration = duration;
        }

        public static TimeSpan GuessDuration(Vector3 v) {
            var color = (Vector3.Normalize(v) * 255f).ToDrawingColor();
            var saturation = color.GetSaturation();

            if (saturation < 0.02 || saturation > 0.6) {
                // what could it be?
                return TimeSpan.FromSeconds(0.1);
            }

            var hue = color.GetHue();
            if (hue > 150 && hue <= 300) {
                // bluish — modern?
                return TimeSpan.Zero;
            }

            if (hue > 100 && hue <= 150) {
                // green — not very old
                return TimeSpan.FromSeconds(0.1);
            }

            if (hue < 20 || hue > 300) {
                // red — brake lights, but not very saturated?
                return TimeSpan.FromSeconds(0.1);
            }

            // the only colors left are yellowish ones — definitely oldschool
            return TimeSpan.FromSeconds(0.2);
        }

        public void Set(Vector3? color, TimeSpan? duration) {
            if (duration.HasValue) {
                _duration = duration.Value;
            }

            if (color == null) {
                if (_value != null) {
                    _off = true;
                    _smoothChanging = true;
                }
            } else {
                _value = color;
                _off = false;
                _smoothChanging = true;
            }
        }

        public void SetMaterial(IDeviceContextHolder holder, [CanBeNull] IEmissiveMaterial material) {
            if (_smoothChanging) {
                if (_stopwatch == null) {
                    _stopwatch = holder.StartNewStopwatch();
                }

                if (_duration == null) {
                    _duration = GuessDuration(_value ?? new Vector3(1f));
                }

                var delta = _stopwatch.ElapsedSeconds / _duration.Value.TotalSeconds;
                _stopwatch.Reset();

                _position = (float)(_off ? _position - delta : _position + delta).Saturate();

                if (_position == (_off ? 0f : 1f)) {
                    _smoothChanging = false;
                    _stopwatch = null;
                }
            }

            if (_value.HasValue) {
                material?.SetEmissiveNext(_value.Value, _position);
            }
        }
    }
}