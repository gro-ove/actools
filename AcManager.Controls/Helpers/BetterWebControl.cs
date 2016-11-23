using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using Awesomium.Core;
using Awesomium.Windows.Controls;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using ContextMenuEventArgs = Awesomium.Core.ContextMenuEventArgs;

namespace AcManager.Controls.Helpers {
    public class ResourceInterceptor : IResourceInterceptor {
        public static string UserAgent { get; internal set; }

        private static readonly Regex FilterRegex = new Regex(@"^
                https?://(?:
                    googleads\.g\.doubleclick\.net/ |
                    apis\.google\.com/se/0/_/\+1 |
                    pagead2\.googlesyndication\.com/pagead |
                    staticxx\.facebook\.com/connect |
                    syndication\.twitter\.com/i/jot |
                    platform\.twitter\.com/widgets |
                    www\.youtube\.com/subscribe_embed |
                    www\.facebook\.com/connect/ping |
                    www\.facebook\.com/plugins/like\.php )",
                RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

        public virtual bool OnFilterNavigation(NavigationRequest request) {
#if DEBUG
            // Logging.Write("OnFilterNavigation(): " + request.Url.OriginalString);
#endif
            return FilterRegex.IsMatch(request.Url.OriginalString.ToLowerInvariant());
        }

        public ResourceResponse OnRequest(ResourceRequest request) {
            if (UserAgent != null) {
                request.AppendExtraHeader(@"User-Agent", UserAgent);
            }

            return null;
        }
    }

    public class BetterWebControl : WebControl {
        static BetterWebControl() {
            WebCore.ResourceInterceptor = new ResourceInterceptor();
        }

        protected override void OnShowContextMenu(ContextMenuEventArgs e) {
            var menu = new ContextMenu {
                Items = {
                    new MenuItem { Command = NavigationCommands.BrowseBack },
                    new MenuItem { Command = NavigationCommands.BrowseForward },
                    new MenuItem { Command = NavigationCommands.Refresh },
                    new Separator(),
                    new MenuItem { Command = ApplicationCommands.SelectAll }
                }
            };

            if (e.Info.HasLinkURL) {
                menu.Items.Insert(0, new MenuItem {
                    Header = "Open Link In Default Browser",
                    Command = new DelegateCommand<Uri>(WindowsHelper.ViewInBrowser),
                    CommandParameter = e.Info.LinkURL
                });
                menu.Items.Insert(1, new Separator());
            }

            /*var openLinkInNewTab = new MenuItem();
            openLinkInNewTab.Header = "Open Link In New Tab";
            openLinkInNewTab.PreviewMouseLeftButtonDown += openLinkInNewTab_PreviewMouseLeftButtonDown;*/
            


            /*if (e.Info.HasLinkURL) {

                e.Handled = true;
                menu.Items.Add(new Separator());
                menu.Items.Add(openLinkInNewTab);
                menu.IsOpen = true;
            }
            */

            menu.IsOpen = true;
            e.Handled = true;
        }

        //public BetterWebControl() {
        //    Loaded += OnLoaded;
        //}

        //private void OnLoaded(object sender, RoutedEventArgs e) {
        //    Logging.Debug(ContextMenu);
        //    ContextMenu.Style = FindResource(@"ContextMenuStyle") as Style;
        //}

        public static readonly DependencyProperty UserAgentProperty = DependencyProperty.Register(nameof(UserAgent), typeof(string),
                typeof(BetterWebControl), new PropertyMetadata(OnUserAgentChanged));

        [CanBeNull]
        public string UserAgent {
            get { return (string)GetValue(UserAgentProperty); }
            set { SetValue(UserAgentProperty, value); }
        }

        private static void OnUserAgentChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ResourceInterceptor.UserAgent = (string)e.NewValue;
        }

        public static readonly DependencyProperty ObjectForScriptingProperty = DependencyProperty.Register(nameof(ObjectForScripting), typeof(object),
                typeof(BetterWebControl), new PropertyMetadata(OnObjectForScriptingChanged));

        [CanBeNull]
        public object ObjectForScripting {
            get { return GetValue(ObjectForScriptingProperty); }
            set { SetValue(ObjectForScriptingProperty, value); }
        }

        private static void OnObjectForScriptingChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((BetterWebControl)o).SetExternal(e.NewValue);
        }

        private void SetExternal(object o) {
            if (!IsDocumentReady || o == null) return;
            using (JSObject interop = CreateGlobalJavascriptObject(@"external")) {
                o.GetType().GetMethods().ToList().ForEach(method => interop.Bind(method.Name, Handler));
            }
        }

        protected override void OnDocumentReady(DocumentReadyEventArgs e) {
            if (e.ReadyState == DocumentReadyState.Ready) {
                SetExternal(ObjectForScripting);
            }

            base.OnDocumentReady(e);
        }
        
        private static object ConvertArgument(JSValue a) {
            if (a.IsNaN) return double.NaN;
            if (a.IsDouble) return (double)a;
            if (a.IsInteger) return (double)(int)a;
            if (a.IsString) return (string)a;
            if (a.IsArray) return ((JSValue[])a).Select(ConvertArgument).ToArray();
            if (a.IsBoolean) return (bool)a;
            if (a.IsNull || a.IsUndefined) return null;
            return a.ToString();
        }

        private static JSValue ConvertResult(object r) {
            if (r == null) return JSValue.Null;

            var s = r as string;
            if (s != null) return s;

            var n = r as IEnumerable<string>;
            if (n != null) return new JSValue(n.Select(ConvertResult).ToArray());

            var l = r as IList;
            if (l != null) return new JSValue(l.OfType<object>().Select(ConvertResult).ToArray());

            if (r is double) return (double)r;
            if (r is int) return (int)r;
            if (r is bool) return (bool)r;

            return r.ToString();
        }

        private JSValue Handler(object sender, JavascriptMethodEventArgs e) {
            var o = ObjectForScripting;
            if (o == null) return JSValue.Undefined;
            var m = o.GetType().GetMethod(e.MethodName);
            return m != null ? ConvertResult(m.Invoke(o, e.Arguments.Select(ConvertArgument).ToArray())) : JSValue.Undefined;
        }
    }
}
