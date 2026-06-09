using System;
using JetBrains.Annotations;

namespace AcManager.Tools.Miscellaneous {
    public class CupEventArgs : EventArgs {
        [NotNull]
        public readonly CupClient.CupKey Key;

        [CanBeNull]
        public readonly CupClient.CupInformation Information;

        public CupEventArgs([NotNull] CupClient.CupKey key, [CanBeNull] CupClient.CupInformation information) {
            Key = key;
            Information = information;
        }

        public override string ToString() {
            return $"CupEventArgs<{Key.Type}, {Key.Id}, v={Information?.Version ?? @"?"}>";
        }
    }
}