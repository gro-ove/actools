using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Controls.UserControls {
    public interface ICustomStyleProvider {
        string GetStyle(string url);
    }

    internal static class CustomStyleProviderExtension {
        [CanBeNull, ContractAnnotation(@"provider:null => null; url:null => null")]
        public static string ToScript([CanBeNull] this ICustomStyleProvider provider, [CanBeNull] string url) {
            if (provider == null || url == null) return null;

            var style = provider.GetStyle(url);
            if (style == null) return null;

            return $@"
var s = document.getElementById('__cm_style');
if (s && s.parentNode) s.parentNode.removeChild(s);
s = document.createElement('style');
s.id = '__cm_style';
s.innerHTML = {JsonConvert.SerializeObject(style)};
if (document.body){{
    document.body.appendChild(s);
}} else if (document.head){{
    var p = document.createElement('style');
    p.innerHTML = 'body{{display:none!important}}html{{background:black!important}}'
    document.head.appendChild(p);

    function onload(){{
        if (s.parentNode == document.head){{
            document.head.removeChild(p);
            document.head.removeChild(s);
            document.body.appendChild(s);
        }}
    }}

    document.head.appendChild(s);
    document.addEventListener('DOMContentLoaded', onload, false);
    window.addEventListener('load', onload, false);
}}";
        }
    }

    internal interface IWebSomething {
        FrameworkElement Initialize();

        event EventHandler<PageLoadingEventArgs> Navigating;

        event EventHandler<PageLoadedEventArgs> Navigated;

        [NotNull]
        string GetUrl();

        void SetScriptProvider([CanBeNull] ScriptProviderBase provider);

        void SetUserAgent([NotNull] string userAgent);

        void SetStyleProvider([CanBeNull] ICustomStyleProvider provider);

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
    }
}