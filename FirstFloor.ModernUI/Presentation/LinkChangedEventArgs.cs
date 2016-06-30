using System;

namespace FirstFloor.ModernUI.Presentation {
    public class LinkChangedEventArgs : EventArgs {
        public LinkChangedEventArgs(string oldValue, string newValue) {
            OldValue = oldValue;
            NewValue = newValue;
        }

        public string OldValue { get; }

        public string NewValue { get; }
    }
}