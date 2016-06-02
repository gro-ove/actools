using System;

namespace AcManager.Controls {
    internal class ChangedPresetEventArgs : EventArgs {
        public string Key { get; }

        public string Value { get; }

        public ChangedPresetEventArgs(string key, string value) {
            Key = key;
            Value = value;
        }
    }
}