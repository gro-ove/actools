using System;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.AcObjectsNew {
    public class AcPlaceholderNew : NotifyPropertyChanged, IAcObjectNew, IWithId {
        public string Id { get; }

        public virtual string DisplayName => Id;

        public virtual bool Enabled { get; }

        public override string ToString() {
            return Id;
        }

        internal AcPlaceholderNew(string id, bool enabled) {
            Id = id;
            Enabled = enabled;
        }

        public virtual int CompareTo(AcPlaceholderNew o) {
            return Enabled == o.Enabled ?
                    string.Compare(DisplayName, o.DisplayName, StringComparison.CurrentCultureIgnoreCase) : Enabled ? -1 : 1;
        }
    }
}
