using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace FirstFloor.ModernUI.Windows.Navigation {
    /// <summary>
    /// The default link navigator with support for loading frame content, external link navigation using the default browser and command execution.
    /// </summary>
    public class DefaultLinkNavigator : ILinkNavigator {
        private CommandDictionary _commands = new CommandDictionary();
        private string[] _externalSchemes = { Uri.UriSchemeHttp, Uri.UriSchemeHttps, Uri.UriSchemeMailto };

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultLinkNavigator"/> class.
        /// </summary>
        public DefaultLinkNavigator() {
            // register navigation _commands
            _commands.Add(new Uri("cmd://browseback"), NavigationCommands.BrowseBack);
            _commands.Add(new Uri("cmd://refresh"), NavigationCommands.Refresh);

            // register application _commands
            _commands.Add(new Uri("cmd://copy"), ApplicationCommands.Copy);
        }

        /// <summary>
        /// Gets or sets the schemes for external link navigation.
        /// </summary>
        /// <remarks>
        /// Default schemes are http, https and mailto.
        /// </remarks>
        public string[] ExternalSchemes {
            get { return _externalSchemes; }
            set { _externalSchemes = value; }
        }

        /// <summary>
        /// Gets or sets the navigable _commands.
        /// </summary>
        public CommandDictionary Commands {
            get { return _commands; }
            set { _commands = value; }
        }

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

            // first check if uri refers to a command
            ICommand command;
            if (_commands != null && _commands.TryGetValue(uri, out command)) {
                // note: not executed within BbCodeBlock context, Hyperlink instance has Command and CommandParameter set
                if (command.CanExecute(parameter)) {
                    command.Execute(parameter);
                }
            } else if (uri.IsAbsoluteUri && _externalSchemes != null && _externalSchemes.Any(s => uri.Scheme.Equals(s, StringComparison.OrdinalIgnoreCase))) {
                // uri is external, load in default browser
                Process.Start(uri.AbsoluteUri);
            } else {
                // perform frame navigation
                if (source == null) {   // source required
                    throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture, Resources.NavigationFailedSourceNotSpecified, uri));
                }

                // use optional parameter as navigation target to identify target frame (_self, _parent, _top or named target frame)
                var frame = NavigationHelper.FindFrame(parameter, source);
                if (frame == null) {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture, Resources.NavigationFailedFrameNotFound, uri, parameter));
                }

                // delegate navigation to the frame
                frame.Source = uri;
            }
        }
    }
}
