using System;

namespace AcManager.Tools.Miscellaneous {
    public class CupEventArgs : EventArgs {
        public readonly CupClient.CupKey Key;
        public readonly CupClient.CupInformation Information;

        public CupEventArgs(CupClient.CupKey key, CupClient.CupInformation information) {
            Key = key;
            Information = information;
        }
    }
}