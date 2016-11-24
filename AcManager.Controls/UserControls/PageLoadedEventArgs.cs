using System;
using FirstFloor.ModernUI.Dialogs;

namespace AcManager.Controls.UserControls {
    public class PageLoadingEventArgs : EventArgs {
        public PageLoadingEventArgs(AsyncProgressEntry progress) {
            Progress = progress;
        }

        public static PageLoadingEventArgs Indetermitate => new PageLoadingEventArgs(AsyncProgressEntry.Indetermitate);

        public static PageLoadingEventArgs Ready => new PageLoadingEventArgs(AsyncProgressEntry.Ready);

        public AsyncProgressEntry Progress { get; }
    }

    public class PageLoadedEventArgs : EventArgs {
        public PageLoadedEventArgs(string url) {
            Url = url;
        }

        public string Url { get; }
    }
}