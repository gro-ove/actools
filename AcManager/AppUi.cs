using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.Managers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Application = System.Windows.Application;

namespace AcManager {
    public class AppUi {
        [NotNull]
        private readonly Application _application;

        public AppUi([NotNull] Application application) {
            _application = application ?? throw new ArgumentNullException(nameof(application));

            // Extra close-if-nothing-shown timer just to be sure
            _timer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, OnTimer, _application.Dispatcher);
            _timer.Start();
        }

        private int _nothing;
        private void OnTimer(object sender, EventArgs eventArgs) {
            if (_application.Windows.Count == 0 && System.Windows.Forms.Application.OpenForms.Count == 0) {
                if (_nothing > 2) {
                    Logging.Debug("Nothing shown! Existing…");
                    _timer.Stop();
                    _application.Shutdown();
                } else {
                    _nothing++;
                }
            } else {
                _nothing = 0;
            }
        }

        private readonly DispatcherTimer _timer;
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
            if (AcRootDirectory.Instance?.IsReady != true) return;

            _additionalProcessing++;
            var v = await ArgumentsHandler.ProcessArguments(obj);
            _showMainWindow |= v != ArgumentsHandler.ShowMainWindow.No;
            Logging.Debug("Show main window: " + _showMainWindow);

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
            ((Action)(async () => {
                EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));

                try {
                    if ((!Superintendent.Instance.IsReady || AcRootDirectorySelector.IsReviewNeeded()) &&
                            new AcRootDirectorySelector().ShowDialog() != true) {
                        Logging.Debug("AC root selection cancelled, exit");
                        return;
                    }

                    if (!AppArguments.Values.Any() || await ArgumentsHandler.ProcessArguments(AppArguments.Values) != ArgumentsHandler.ShowMainWindow.No) {
                        _showMainWindow = true;
                    }

                    if (_additionalProcessing > 0) {
                        Logging.Debug("Waiting for extra workers…");
                        await WaitForInProgress();
                        Logging.Debug("Done");
                    }

                    if (_showMainWindow) {
                        Logging.Debug("Main window…");
                        await new MainWindow().ShowAndWaitAsync();
                        Logging.Debug("Main window closed");
                    }

                    Logging.Debug("Waiting for extra windows to close…");

                    do {
                        await Task.Delay(100);
                    } while (await WaitForWindowToClose(_application.Windows.OfType<DpiAwareWindow>().FirstOrDefault(x => x.IsVisible)));

                    Logging.Debug("No more windows");
                } finally {
                    _timer.Stop();
                    _application.Shutdown();
                }

                Logging.Debug("Main loop is finished");
            })).InvokeInMainThreadAsync();
        }
    }
}