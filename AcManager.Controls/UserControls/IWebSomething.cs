using System;
using System.Windows;

namespace AcManager.Controls.UserControls {
    internal interface IWebSomething {
        FrameworkElement Initialize();

        event EventHandler<PageLoadedEventArgs> Navigated;

        string GetUrl();

        void SetScriptProvider(ScriptProviderBase provider);

        void SetUserAgent(string userAgent);

        void ModifyPage();

        void Execute(string js);

        void Navigate(string url);

        void GoBack();

        bool CanGoBack();

        void GoForward();

        bool CanGoForward();

        void OnLoaded();

        void OnUnloaded();
    }
}