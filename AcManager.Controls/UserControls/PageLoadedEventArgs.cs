using System;

namespace AcManager.Controls.UserControls {
    public class PageLoadedEventArgs : EventArgs {
        public PageLoadedEventArgs(string url) {
            Url = url;
        }

        public string Url { get; }
    }
}