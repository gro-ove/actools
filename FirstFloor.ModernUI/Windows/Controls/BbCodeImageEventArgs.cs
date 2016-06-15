using System;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class BbCodeImageEventArgs : EventArgs {
        public readonly Uri ImageUri;

        public BbCodeImageEventArgs(Uri imageUri) {
            ImageUri = imageUri;
        }
    }
}