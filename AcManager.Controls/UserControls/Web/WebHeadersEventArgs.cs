using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.Web {
    public class WebHeadersEventArgs : EventArgs {
        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>();

        public WebHeadersEventArgs([NotNull] string url) {
            Url = url;
        }

        [NotNull]
        public string Url { get; }
    }
}