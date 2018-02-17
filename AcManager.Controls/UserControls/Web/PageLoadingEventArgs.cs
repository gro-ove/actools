using System;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.Web {
    public class PageLoadingEventArgs : EventArgs {
        public PageLoadingEventArgs(AsyncProgressEntry progress, [CanBeNull] string url) {
            Progress = progress;
            Url = url;
        }

        public AsyncProgressEntry Progress { get; }

        [CanBeNull]
        public string Url { get; }
    }
}