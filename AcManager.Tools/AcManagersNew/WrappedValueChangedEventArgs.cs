using System;
using AcManager.Tools.AcObjectsNew;
using JetBrains.Annotations;

namespace AcManager.Tools.AcManagersNew {
    public class WrappedValueChangedEventArgs : EventArgs {
        [NotNull]
        public readonly AcPlaceholderNew OldValue;

        [NotNull]
        public readonly AcPlaceholderNew NewValue;

        public WrappedValueChangedEventArgs([NotNull]AcPlaceholderNew oldValue, [NotNull]AcPlaceholderNew newValue) {
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}