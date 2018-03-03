using System;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.Web {
    public class FaviconChangedEventArgs : EventArgs {
        public FaviconChangedEventArgs([CanBeNull] string url) {
            Url = url;
        }

        [CanBeNull]
        public string Url { get; }
    }
}