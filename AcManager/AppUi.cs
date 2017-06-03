using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager {
    public class AppUi {
        [NotNull]
        private readonly Application _application;

        public AppUi([NotNull] Application application) {
            _application = application ?? throw new ArgumentNullException(nameof(application));
        }

        private Window _currentWindow;
        private int _additionalProcessing;
        private bool _showMainWindow;
        private TaskCompletionSource<bool> _waiting;

        private void SetCurrentWindow(Window window) {
            void OnUnloaded(object o, RoutedEventArgs routedEventArgs) {
                SetCurrentWindow(_application.Windows.OfType<Window>().ApartFrom(_currentWindow).FirstOrDefault());
            }

            if (_currentWindow != null) {
                _currentWindow.Unloaded -= OnUnloaded;
            }

            _currentWindow = window;
            if (_currentWindow != null) {
                EntryPoint.HandleSecondInstanceMessages(_currentWindow, HandleMessagesAsync);
                _currentWindow.Unloaded += OnUnloaded;
            }
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e) {
            var window = sender as Window;
            if (window == null) return;

            if (_currentWindow == null) {
                SetCurrentWindow(window);
            }
        }

        private Task WaitForInProgress() {
            return (_waiting ?? (_waiting = new TaskCompletionSource<bool>())).Task;
        }

        private async void HandleMessagesAsync(IEnumerable<string> obj) {
            _additionalProcessing++;
            _showMainWindow |= await ArgumentsHandler.ProcessArguments(obj);
            if (--_additionalProcessing == 0) {
                _waiting?.SetResult(true);
            }
        }

        private static Task<bool> WaitForWindowToClose(Window window) {
            if (window == null) return Task.FromResult(false);
            var task = new TaskCompletionSource<bool>();
            window.Closed += (s, a) => task.SetResult(true);
            return task.Task;
        }

        public void Run() {
            ActionExtension.InvokeInMainThreadAsync(async () => {
                EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));

                try {
                    if (!AppArguments.Values.Any() || await ArgumentsHandler.ProcessArguments(AppArguments.Values)) {
                        _showMainWindow = true;
                    }

                    if (_additionalProcessing > 0) {
                        await WaitForInProgress();
                    }

                    if (_showMainWindow) {
                        await new MainWindow().ShowAndWaitAsync();
                    }

                    do {
                        await Task.Delay(100);
                    } while (await WaitForWindowToClose(_application.Windows.OfType<DpiAwareWindow>().FirstOrDefault()));
                } finally {
                    _application.Shutdown();
                }
            });
        }
    }
}