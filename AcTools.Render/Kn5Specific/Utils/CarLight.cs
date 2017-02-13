using AcTools.Render.Base.Objects;
using AcTools.Render.Data;
using AcTools.Render.Kn5Specific.Objects;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Utils {
    public class CarLight {
        [CanBeNull]
        public CarData.LightObject Description { get; set; }

        private bool _isEnabled;

        public bool IsHeadlightEnabled {
            get { return _isEnabled; }
            set {
                if (Equals(_isEnabled, value)) return;
                _isEnabled = value;
                UpdateEmissive();
            }
        }

        private bool _isBrakeEnabled;

        public bool IsBrakeEnabled {
            get { return _isBrakeEnabled; }
            set {
                if (Equals(value, _isBrakeEnabled)) return;
                _isBrakeEnabled = value;
                UpdateEmissive();
            }
        }

        private void UpdateEmissive() {
            if (!IsHeadlightEnabled && !IsBrakeEnabled || Description == null) {
                SetEmissive(default(Vector3));
            } else if (IsHeadlightEnabled) {
                if (IsBrakeEnabled) {
                    SetEmissive(Description.HeadlightColor.LengthSquared() > Description.BrakeColor.LengthSquared() ?
                            Description.HeadlightColor : Description.BrakeColor);
                } else {
                    SetEmissive(Description.HeadlightColor);
                }
            } else if (IsBrakeEnabled) {
                SetEmissive(Description.BrakeColor);
            }
        }

        protected virtual void SetEmissive(Vector3 value) {
            Node?.SetEmissive(value);
        }

        [CanBeNull]
        protected IKn5RenderableObject Node { get; private set; }

        public virtual void Initialize([NotNull] CarData.LightObject description, RenderableList main) {
            Description = description;
            Node = main.GetByName(description.Name);
        }
    }
}