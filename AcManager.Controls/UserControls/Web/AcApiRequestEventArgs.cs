using System;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.Web {
    public class AcApiRequestEventArgs : EventArgs {
        public AcApiRequestEventArgs([NotNull] string requestUrl) {
            RequestUrl = requestUrl;
        }

        [NotNull]
        public string RequestUrl { get; }
        
        [CanBeNull]
        public string Response { get; set; }
    }
}