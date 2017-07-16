using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AcManager.Controls.UserControls;
using AcManager.Tools.Helpers;
using Awesomium.Core;
using Awesomium.Windows.Controls;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using ContextMenuEventArgs = Awesomium.Core.ContextMenuEventArgs;

namespace AcManager.Controls.Helpers {
    public class ResourceInterceptor : IResourceInterceptor {
        public static string UserAgent { get; internal set; }

        public bool OnFilterNavigation(NavigationRequest request) {
            return RequestsFiltering.ShouldBeBlocked(request.Url.OriginalString.ToLowerInvariant());
        }

        public ResourceResponse OnRequest(ResourceRequest request) {
            if (UserAgent != null) {
                request.AppendExtraHeader(@"User-Agent", UserAgent);
            }

            return null;
        }
    }

    internal static class InsertRangeExtension {
        public static void InsertRange<T>(this IList collection, int index, params T[] range) {
            for (var i = 0; i < range.Length; i++) {
                collection.Insert(index + i, range[i]);
            }
        }
    }

    public class BetterWebControl : WebControl {
        static BetterWebControl() {
            WebCore.ResourceInterceptor = new ResourceInterceptor();
        }

        public void Navigate(string url) {
            try {
                Source = new Uri(url, UriKind.RelativeOrAbsolute);
            } catch (Exception e) {
                if (!url.StartsWith(@"http://", StringComparison.OrdinalIgnoreCase) &&
                        !url.StartsWith(@"https://", StringComparison.OrdinalIgnoreCase)) {
                    url = @"http://" + url;
                    try {
                        Source = new Uri(url, UriKind.RelativeOrAbsolute);
                    } catch (Exception ex) {
                        Logging.Write("Navigation failed: " + ex);
                    }
                } else {
                    Logging.Write("Navigation failed: " + e);
                }
            }
        }

        protected override void OnShowContextMenu(ContextMenuEventArgs e) {
            var menu = new ContextMenu {
                Items = {
                    new MenuItem {
                        Header = "Back",
                        Command = new DelegateCommand(GoBack, CanGoBack)
                    },
                    new MenuItem {
                        Header = "Forward",
                        Command = new DelegateCommand(GoForward, CanGoForward)
                    },
                    new MenuItem {
                        Header = "Refresh",
                        Command = new DelegateCommand(() => Reload(true))
                    },
                    new Separator(),
                    new MenuItem {
                        Header = "Select All",
                        Command = new DelegateCommand(SelectAll)
                    },
                    new MenuItem {
                        Header = "Open Page In Default Browser",
                        Command = new DelegateCommand<Uri>(WindowsHelper.ViewInBrowser),
                        CommandParameter = e.Info.PageURL
                    },
                }
            };

            if (e.Info.HasSelection) {
                menu.Items.InsertRange<Control>(0, new MenuItem {
                    Header = "Copy",
                    Command = new DelegateCommand(Copy),
                }, new Separator());
            }

            if (e.Info.HasLinkURL) {
                menu.Items.InsertRange<Control>(0, new MenuItem {
                    Header = "Open Link",
                    Command = new DelegateCommand<Uri>(u => {
                        Navigate(u.ToString());
                    }),
                    CommandParameter = e.Info.LinkURL
                }, new MenuItem {
                    Header = "Open Link In Default Browser",
                    Command = new DelegateCommand<Uri>(WindowsHelper.ViewInBrowser),
                    CommandParameter = e.Info.LinkURL
                }, new MenuItem {
                    Header = "Copy Link Address",
                    Command = new DelegateCommand(CopyLinkAddress),
                }, new Separator());
            }

            menu.IsOpen = true;
            e.Handled = true;
        }

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
