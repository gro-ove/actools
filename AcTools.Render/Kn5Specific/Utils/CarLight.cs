using System.Linq;
using AcTools.DataFile;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Utils {
    public class CarLight {
        public CarLightType Type { get; private set; }

        public string Name { get; private set; }

        public Vector3 Emissive { get; private set; }

        public bool IsEnabled {
            get { return _isEnabled; }
            set {
                if (Equals(_isEnabled, value)) return;

                _isEnabled = value;
                OnEnabledChanged(_isEnabled);
            }
        }

        protected virtual void OnEnabledChanged(bool value) {
            Node?.SetEmissive(_isEnabled ? Emissive : (Vector3?)null);
        }

        [CanBeNull]
        public Kn5RenderableObject Node { get; private set; }

        private bool _isEnabled;

        public virtual void Initialize(CarLightType type, Kn5RenderableList main, IniFileSection section) {
            Type = type;
            Name = section.Get("NAME");
            Emissive = section.GetVector3("COLOR").Select(y => (float)y).ToArray().ToVector3();
            Node = main.GetByName(Name);
        }
    }
}