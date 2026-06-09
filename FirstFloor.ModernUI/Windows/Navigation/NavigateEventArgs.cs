using System;
using System.ComponentModel;

namespace FirstFloor.ModernUI.Windows.Navigation {
    public class NavigateEventArgs : CancelEventArgs {
        public NavigateEventArgs(Uri uri) {
            Uri = uri;
        }

        public Uri Uri { get; }
    }
}