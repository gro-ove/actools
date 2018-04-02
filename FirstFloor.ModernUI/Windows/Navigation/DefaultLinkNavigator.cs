using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Navigation {
    public interface INavigateUriHandler {
        bool NavigateTo(Uri uri);
    }

    /// <summary>
    /// The default link navigator with support for loading frame content, external link navigation using the default browser and command execution.
    /// </summary>
    public class DefaultLinkNavigator : ILinkNavigator {
        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultLinkNavigator"/> class.
        /// </summary>
        public DefaultLinkNavigator() {
            // register navigation _commands
            Commands.Add(new Uri("cmd://browseback"), NavigationCommands.BrowseBack);
            Commands.Add(new Uri("cmd://refresh"), NavigationCommands.Refresh);

            // register application _commands
            Commands.Add(new Uri("cmd://copy"), ApplicationCommands.Copy);
        }

        /// <summary>
        /// Gets or sets the schemes for external link navigation.
        /// </summary>
        /// <remarks>
        /// Default schemes are http, https and mailto.
        /// </remarks>
        public  string[] ExternalSchemes { get; set; } = { Uri.UriSchemeHttp, Uri.UriSchemeHttps, Uri.UriSchemeMailto };

        /// <summary>
        /// Gets or sets the navigable _commands.
        /// </summary>
        public CommandDictionary Commands { get; set; } = new CommandDictionary();

        public event EventHandler<NavigateEventArgs> PreviewNavigate;

        /// <summary>
        /// Performs navigation to specified link.
        /// </summary>
        /// <param name="uri">The uri to navigate to.</param>
        /// <param name="source">The source element that triggers the navigation. Required for frame navigation.</param>
        /// <param name="parameter">An optional command parameter or navigation target.</param>
        public virtual void Navigate(Uri uri, FrameworkElement source = null, string parameter = null) {
            if (uri == null) {
                throw new ArgumentNullException(nameof(uri));
            }

            if (uri.OriginalString.StartsWith(@"www.")) {
                uri = new Uri("http://" + uri.OriginalString);
            }

            var args = new NavigateEventArgs(uri);
            PreviewNavigate?.Invoke(this, args);
            if (args.Cancel) return;

            // first check if uri refers to a command
            if (Commands != null) {
                if (Commands.TryGetValue(uri, out var command)) {
                    // note: not executed within BbCodeBlock context, Hyperlink instance has Command and CommandParameter set
                    if (command.CanExecute(parameter)) {
                        command.Execute(parameter);
                    }
                    return;
                }

                if (uri.IsAbsoluteUri) {
                    var original = uri.AbsoluteUri;
                    var index = original.IndexOf('?');
                    if (index != -1) {
                        var subUri = new Uri(original.Substring(0, index), UriKind.Absolute);
                        if (Commands.TryGetValue(subUri, out command)) {
                            parameter = uri.GetQueryParam("param");
                            if (command.CanExecute(parameter)) {
                                command.Execute(parameter);
                            }
                            return;
                        }
                    }
                }
            }

            if (uri.IsAbsoluteUri && ExternalSchemes != null && ExternalSchemes.Any(s => uri.Scheme.Equals(s, StringComparison.OrdinalIgnoreCase))) {
                // uri is external, load in default browser
                Process.Start(uri.AbsoluteUri);
            } else {
                // perform frame navigation
                if (source == null) {   // source required
                    throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture, UiStrings.NavigationFailedSourceNotSpecified, uri));
                }

                // use optional parameter as navigation target to identify target frame (_self, _parent, _top or named target frame)
                var frame = NavigationHelper.FindFrame(parameter, source);
                if (frame == null) {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture, UiStrings.NavigationFailedFrameNotFound, uri, parameter));
                }

                if (!(Window.GetWindow(frame) is INavigateUriHandler window) || frame.GetParent<ModernFrame>() != null || window.NavigateTo(uri) != true) {
                    // delegate navigation to the frame
                    frame.Source = uri;
                }
            }
        }
    }
}
