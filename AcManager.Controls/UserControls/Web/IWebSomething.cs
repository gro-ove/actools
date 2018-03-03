using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.Web {
    internal interface IWebSomething {
        [NotNull]
        FrameworkElement GetElement([CanBeNull] DpiAwareWindow parentWindow, bool preferTransparentBackground);

        event EventHandler<UrlEventArgs> PageLoadingStarted;
        event EventHandler<UrlEventArgs> PageLoaded;
        event EventHandler<PageLoadingEventArgs> LoadingStateChanged;
        event EventHandler<NewWindowEventArgs> NewWindow;
        event EventHandler<UrlEventArgs> AddressChanged;
        event EventHandler<TitleChangedEventArgs> TitleChanged;
        event EventHandler<FaviconChangedEventArgs> FaviconChanged;

        bool SupportsFavicons { get; }

        [NotNull]
        string GetUrl();

        [CanBeNull]
        T GetJsBridge<T>(Func<T> factory) where T : JsBridgeBase;

        void SetUserAgent([NotNull] string userAgent);
        void SetStyleProvider([CanBeNull] ICustomStyleProvider provider);
        void SetDownloadListener([CanBeNull] IWebDownloadListener listener);
        void SetNewWindowsBehavior(NewWindowsBehavior mode);

        void Execute([NotNull] string js);
        void Navigate([NotNull] string url);

        void OnLoaded();
        void OnUnloaded();
        void OnError(string error, string url, int line, int column);

        [ItemCanBeNull]
        Task<string> GetImageUrlAsync([CanBeNull] string filename);

        ICommand BackCommand { get; }
        ICommand ForwardCommand { get; }
        ICommand RefreshCommand { get; }

        // Unfortunately, here, standard Windows’ WebBrowser becomes completely unusable. Thankfully,
        // in most cases those features aren’t needed.

        bool CanHandleAcApiRequests { get; }
        event EventHandler<AcApiRequestEventArgs> AcApiRequest;

        bool IsInjectSupported { get; }
        event EventHandler<WebInjectEventArgs> Inject;

        bool CanConvertFilenames { get; }

        [ContractAnnotation(@"filename: null => null; filename: notnull => notnull")]
        string ConvertFilename([CanBeNull] string filename);
    }
}