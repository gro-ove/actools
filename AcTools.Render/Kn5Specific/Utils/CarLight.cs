using System;
using AcTools.Render.Base.Objects;
using AcTools.Render.Data;
using AcTools.Render.Kn5Specific.Objects;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Utils {
    public class CarLight {
        [CanBeNull]
        public CarData.LightObject Description { get; set; }

        private bool? _isEnabled;

        public bool IsHeadlightEnabled {
            get { return _isEnabled ?? false; }
            set {
                if (Equals(_isEnabled, value)) return;
                _isEnabled = value;
                UpdateEmissive();
            }
        }

        private bool? _isBrakeEnabled;

        public bool IsBrakeEnabled {
            get { return _isBrakeEnabled ?? false; }
            set {
                if (Equals(value, _isBrakeEnabled)) return;
                _isBrakeEnabled = value;
                UpdateEmissive();
            }
        }

        private void UpdateEmissive() {
            var d = Description;
            if (!IsHeadlightEnabled && !IsBrakeEnabled || d == null) {
                SetEmissive(null, d?.Duration);
            } else if (IsHeadlightEnabled) {
                if (IsBrakeEnabled) {
                    SetEmissive(d.HeadlightColor == null || d.BrakeColor != null &&
                            d.BrakeColor.Value.LengthSquared() > d.HeadlightColor.Value.LengthSquared() ?
                            d.BrakeColor : d.HeadlightColor, d.Duration);
                } else {
                    SetEmissive(d.HeadlightColor, d.Duration);
                }
            } else if (IsBrakeEnabled) {
                SetEmissive(d.BrakeColor, d.Duration);
            }
        }

        protected virtual void SetEmissive(Vector3? value, TimeSpan? duration) {
            Node?.Emissive.Set(value, duration);
        }

        public TimeSpan? GetDuration() {
            var d = Description;
            if (d == null) return null;
            if (d.Duration.HasValue) return d.Duration;

            var color = d.BrakeColor ?? d.HeadlightColor;
            if (!color.HasValue) return null;

            return SmoothEmissiveChange.GuessDuration(color.Value);
        }

        [CanBeNull]
        protected IKn5RenderableObject Node { get; private set; }

        public void Initialize([NotNull] CarData.LightObject description, RenderableList main) {
            Description = description;
            Node = main.GetByName(description.Name);
        }
    }
}