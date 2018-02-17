using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls.UserControls.CefSharp;
using AcManager.Controls.UserControls.Web;
using AcManager.Tools.Managers.Plugins;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Controls.UserControls {
    public class WebTab : NotifyPropertyChanged {
        private static Dictionary<string, IWebSomething> _somethings = new Dictionary<string, IWebSomething>();

        [NotNull]
        private static IWebSomething GetSomething() {
            if (PluginsManager.Instance.IsPluginEnabled(KnownPlugins.CefSharp)) return new CefSharpWrapper();
            return new WebBrowserWrapper();
        }

        [NotNull]
        internal IWebSomething Something { get; }

        public FrameworkElement Element { get; }

        public WebTab(string url) {
            Something = GetSomething();
            Something.Navigating += OnProgressChanged;
            Something.Navigated += OnNavigated;
            Something.NewWindow += OnNewWindow;
            Something.TitleChanged += OnTitleChanged;
            Element = Something.GetElement();
            Something.OnLoaded();
            Title = url;
            LoadedUrl = ActiveUrl = url;
            Navigate(url);
        }

        private string _title;

        public string Title {
            get => _title;
            private set => Apply(value, ref _title);
        }

        private void OnTitleChanged(object sender, TitleChangedEventArgs e) {
            Title = e.Title;
        }

        public ICommand BackCommand => Something.BackCommand;
        public ICommand ForwardCommand => Something.ForwardCommand;
        public ICommand RefreshCommand => Something.RefreshCommand;

        public void Navigate([CanBeNull] string url) {
            Something.Navigate(url ?? @"about:blank");
        }

        [ItemCanBeNull]
        public Task<string> GetImageUrlAsync([CanBeNull] string filename) {
            return Something.GetImageUrlAsync(filename) ?? Task.FromResult((string)null);
        }

        public void OnError(string error, string url, int line, int column) {
            Something.OnError(error, url, line, column);
        }

        private string _loadedUrl;

        public string LoadedUrl {
            get => _loadedUrl;
            private set => Apply(value, ref _loadedUrl);
        }

        private string _activeUrl;

        public string ActiveUrl {
            get => _activeUrl;
            private set => Apply(value, ref _activeUrl, () => {
                Favicon = Regex.Replace(value, @"(?<=\w)/.+", "") + @"/favicon.ico";
            });
        }

        private string _favicon;

        public string Favicon {
            get => _favicon;
            private set => Apply(value, ref _favicon);
        }

        public void Execute(string js, bool onload = false) {
            try {
                Something.Execute(onload ?
                        @"(function(){ var f = function(){" + js + @"}; if (!document.body) window.addEventListener('load', f, false); else f(); })();" :
                        @"(function(){" + js + @"})();");
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        public void Execute(string fnName, params object[] args) {
            Execute(fnName, false, args);
        }

        public void Execute(string fnName, bool onload, params object[] args) {
            var js = $"{fnName}({args.Select(JsonConvert.SerializeObject).JoinToString(',')})";
            Execute(js, onload);
        }

        private void OnNavigated(object sender, UrlEventArgs e) {
            CommandManager.InvalidateRequerySuggested();
            LoadedUrl = e.Url;
            ActiveUrl = e.Url;
            PageLoaded?.Invoke(this, new UrlEventArgs(Something.GetUrl()));
        }

        public event EventHandler<UrlEventArgs> PageLoaded;

        private void OnNewWindow(object sender, UrlEventArgs e) {
            NewWindow?.Invoke(this, e);
        }

        public event EventHandler<UrlEventArgs> NewWindow;

        private bool _isLoading;

        public bool IsLoading {
            get => _isLoading;
            private set => Apply(value, ref _isLoading);
        }

        private void OnProgressChanged(object sender, PageLoadingEventArgs e) {
            ActiveUrl = e.Url;
            IsLoading = !e.Progress.IsReady;
        }
    }
}