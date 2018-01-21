using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using AcManager.Tools.Miscellaneous;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace AcManager.Controls.Helpers {
    public class LocalKeyBindingsController : NotifyPropertyChanged {
        [CanBeNull]
        private List<ILocalKeyBindingInput> _list;

        public LocalKeyBindingsController(FrameworkElement parentElement) {
            if (parentElement.IsLoaded) {
                SetListeners(parentElement);
            } else {
                parentElement.Loaded += OnLoaded;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs) {
            var parentElement = (FrameworkElement)sender;
            parentElement.Loaded += OnLoaded;
            SetListeners(parentElement);
        }

        private void SetListeners(DependencyObject parentElement) {
            var window = Window.GetWindow(parentElement);
            if (window != null) {
                WeakEventManager<UIElement, KeyEventArgs>.AddHandler(window, nameof(window.PreviewKeyDown), OnKeyDown);
                WeakEventManager<UIElement, KeyEventArgs>.AddHandler(window, nameof(window.PreviewKeyUp), OnKeyUp);
                WeakEventManager<Window, EventArgs>.AddHandler(window, nameof(window.Deactivated), OnFocusLost);
            }
        }

        [CanBeNull]
        public ILocalKeyBindingInput IsWaitingForKey => _list?.FirstOrDefault(x => x.IsWaiting);

        private DelegateCommand<ILocalKeyBindingInput> _toggleWaitingCommand;

        public DelegateCommand<ILocalKeyBindingInput> ToggleWaitingCommand
            => _toggleWaitingCommand ?? (_toggleWaitingCommand = new DelegateCommand<ILocalKeyBindingInput>(o => {
                o.IsWaiting = true;
            }, o => o != null));

        public void Set(IEnumerable<ILocalKeyBindingInput> list) {
            _list = list.ToList();
            OnPropertyChanged(nameof(IsWaitingForKey));
            OnPropertyChanged(nameof(ToggleWaitingCommand));
        }

        private void OnKeyDown(object sender, KeyEventArgs e) {
            if (_list == null) return;

            var key = (Keys)KeyInterop.VirtualKeyFromKey(e.Key);
            ILocalKeyBindingInput waiting = null;
            foreach (var value in _list) {
                value.IsPressed = key == value.Value;
                if (!value.IsWaiting) continue;
                if (waiting == null) {
                    waiting = value;
                } else {
                    value.IsWaiting = false;
                }
            }

            if (waiting == null) return;
            switch (e.Key) {
                case Key.Escape:
                case Key.Back:
                case Key.Enter:
                    waiting.IsWaiting = false;
                    e.Handled = true;
                    break;

                case Key.Delete:
                    waiting.ClearCommand.Execute(null);
                    e.Handled = true;
                    break;

                default:
                    waiting.Value = (Keys)KeyInterop.VirtualKeyFromKey(e.Key);
                    waiting.IsWaiting = false;
                    e.Handled = true;
                    break;
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e) {
            if (_list == null) return;
            foreach (var value in _list) {
                value.IsPressed = false;
            }
        }

        private void OnFocusLost(object sender, EventArgs e) {
            if (_list == null) return;
            foreach (var value in _list) {
                value.IsPressed = false;
            }
        }
    }
}