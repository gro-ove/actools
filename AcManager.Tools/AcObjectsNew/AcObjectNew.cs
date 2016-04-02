using System;
using AcManager.Tools.AcManagersNew;
using JetBrains.Annotations;

namespace AcManager.Tools.AcObjectsNew {
    public abstract class AcObjectNew : AcPlaceholderNew {
        public readonly IAcManagerNew Manager;

        protected AcObjectNew(IAcManagerNew manager, string id, bool enabled)
                : base(id, enabled) {
            Manager = manager;
        }

        public virtual void Reload() {
            Manager.Reload(Id);
        }

        public abstract void Load();

        public virtual void PastLoad() {}

        private string _name;

        [CanBeNull]
        public virtual string Name {
            get { return _name; }
            protected set {
                if (Equals(value, _name)) return;
                _name = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public override string DisplayName => Name ?? Id;

        public override string ToString() {
            return DisplayName;
        }

        public bool Outdated { get; private set; }

        /// <summary>
        /// Call this from AcManager when object is being replaced or something else.
        /// </summary>
        public void Outdate() {
            Outdated = true;
            OnPropertyChanged(nameof(Outdated));
            OnAcObjectOutdated();
        }

        protected virtual void OnAcObjectOutdated() {
            AcObjectOutdated?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler AcObjectOutdated;
    }
}
