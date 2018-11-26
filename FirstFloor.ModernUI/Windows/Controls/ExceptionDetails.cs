using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Navigation;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class ExceptionDetails : Control, ILinkNavigator {
        private ILinkNavigator _baseLinkNavigator;

        public ExceptionDetails() {
            DefaultStyleKey = typeof(ExceptionDetails);
        }

        public static readonly DependencyPropertyKey StackTracePropertyKey = DependencyProperty.RegisterReadOnly(nameof(StackTrace), typeof(string),
                typeof(ExceptionDetails), new PropertyMetadata(null));

        public static readonly DependencyProperty StackTraceProperty = StackTracePropertyKey.DependencyProperty;

        public string StackTrace => (string)GetValue(StackTraceProperty);

        public static readonly DependencyProperty ExceptionProperty = DependencyProperty.Register(nameof(Exception), typeof(Exception),
                typeof(ExceptionDetails), new PropertyMetadata(OnExceptionChanged));

        public Exception Exception {
            get => (Exception)GetValue(ExceptionProperty);
            set => SetValue(ExceptionProperty, value);
        }

        private static void OnExceptionChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ExceptionDetails)o).OnExceptionChanged((Exception)e.NewValue);
        }

        private void OnExceptionChanged(Exception newValue) {
            SetValue(StackTracePropertyKey, newValue == null ? null : string.Join("\n",
                    newValue.ToString().Trim().Replace("\r", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)));
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string),
                typeof(ExceptionDetails), new PropertyMetadata(UiStrings.NavigationFailed));

        public string Title {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty SuggestionsProperty = DependencyProperty.Register(nameof(Suggestions), typeof(string),
                typeof(ExceptionDetails), new PropertyMetadata(UiStrings.SuggestionsMessage));

        public string Suggestions {
            get => (string)GetValue(SuggestionsProperty);
            set => SetValue(SuggestionsProperty, value);
        }

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();

            if (_baseLinkNavigator == null) {
                _baseLinkNavigator = new DefaultLinkNavigator();
                _commands = new CommandDictionary {
                    { new Uri("cmd://browseback"), NavigationCommands.BrowseBack },
                    { new Uri("cmd://refresh"), NavigationCommands.Refresh },
                    { new Uri("cmd://copy"), new DelegateCommand(() => ClipboardHelper.SetText(StackTrace)) }
                };
            }

            (GetTemplateChild(@"PART_Suggestions") as SelectableBbCodeBlock)?.SetValue(SelectableBbCodeBlock.LinkNavigatorProperty, this);
        }

        private CommandDictionary _commands;

        CommandDictionary ILinkNavigator.Commands {
            get => _commands;
            set => _commands = value;
        }

        event EventHandler<NavigateEventArgs> ILinkNavigator.PreviewNavigate {
            add => _baseLinkNavigator.PreviewNavigate += value;
            remove => _baseLinkNavigator.PreviewNavigate -= value;
        }

        void ILinkNavigator.Navigate(Uri uri, FrameworkElement source, string parameter) {
            _baseLinkNavigator.Navigate(uri, source, parameter);
        }
    }
}