using System;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.Web {
    public class WebTabEventArgs : EventArgs {
        public WebTabEventArgs([NotNull] WebTab tab) {
            Tab = tab;
        }

        [NotNull]
        public WebTab Tab { get; }
    }
}