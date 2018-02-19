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

        event EventHandler<PageLoadingEventArgs> Navigating;
        event EventHandler<PageLoadedEventArgs> Navigated;
        event EventHandler<UrlEventArgs> NewWindow;
        event EventHandler<TitleChangedEventArgs> TitleChanged;

        [NotNull]
        string GetUrl();

        void SetJsBridge([CanBeNull] JsBridgeBase bridge);
        void SetUserAgent([NotNull] string userAgent);
        void SetStyleProvider([CanBeNull] ICustomStyleProvider provider);
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