using System;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.Web {
    public class UrlEventArgs : EventArgs {
        public UrlEventArgs([NotNull] string url) {
            Url = url;
        }

        [NotNull]
        public string Url { get; }
    }
}