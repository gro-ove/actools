using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.Web {
    public class PageLoadedEventArgs : UrlEventArgs {
        public PageLoadedEventArgs([NotNull] string url, [CanBeNull] string favicon) : base(url) {
            Favicon = favicon;
        }

        [CanBeNull]
        public string Favicon { get; }
    }
}