using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using AcManager.Controls.Helpers;
using AcManager.Internal;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Managers;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
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
            if (_application.Dispatcher != null) {
                _timer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, OnTimer, _application.Dispatcher);
                _timer.Start();
            }
        }

        private int _nothing;

        private void OnTimer(object sender, EventArgs eventArgs) {
            if (_application.Windows.Count == 0 && System.Windows.Forms.Application.OpenForms.Count == 0) {
                if (_nothing > 2) {
                    Logging.Write("Nothing shown! Existing…");
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
        private bool _showMainWindow, _firstWindowFocused;
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
                EntryPoint.HandleSecondInstanceMessages(_currentWindow, HandleMessages);
                _currentWindow.Unloaded += OnUnloaded;
            }
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e) {
            if (sender is Window window && _currentWindow == null) {
                SetCurrentWindow(window);

                if (!_firstWindowFocused && AppArguments.GetBool(AppFlag.ForcefullyFocusFirstWindow, true)) {
                    _firstWindowFocused = true;
                    User32.SetForegroundWindow(new WindowInteropHelper(window).EnsureHandle());
                }
            }
        }

        private Task WaitForInProgress() {
            return (_waiting ?? (_waiting = new TaskCompletionSource<bool>())).Task;
        }

        private static bool CanGoWithoutWindow(string arg) {
            return arg.EndsWith(".report", StringComparison.OrdinalIgnoreCase)
                    || arg.EndsWith(".report-launch", StringComparison.OrdinalIgnoreCase);
        }

        private bool HandleMessages(IEnumerable<string> obj) {
            if (AcRootDirectory.Instance?.IsReady != true) return false;

            var list = obj.ToList();
            Logging.Write(list.Select(x => $"“{x}”").JoinToReadableString());
            HandleMessagesAsync(list).Ignore();
            return list.Count == 0 || !list.All(CanGoWithoutWindow);
        }

        private bool _mainWindowShown;
        private Task _mainWindowTask;

        private async Task HandleMessagesAsync(IEnumerable<string> obj) {
            _additionalProcessing++;
            var v = await ArgumentsHandler.ProcessArguments(obj, false);
            _showMainWindow |= v != ArgumentsHandler.ShowMainWindow.No;
            Logging.Write("Show main window: " + _showMainWindow);

            ShowMainWindow();

            if (--_additionalProcessing == 0) {
                _waiting?.SetResult(true);
            }
        }

        private void ShowMainWindow() {
            if (_showMainWindow && _mainWindowTask == null && !_mainWindowShown) {
                _mainWindowShown = true;
                _mainWindowTask = ShowMainWindowAsync();
            }
        }

        private void OnTaskAdded(object sender, EventArgs e) {
            _showMainWindow = true;
            ShowMainWindow();
        }

        private static Task<bool> WaitForWindowToClose(Window window) {
            if (window == null) return Task.FromResult(false);
            var task = new TaskCompletionSource<bool>();
            window.Closed += (s, a) => task.SetResult(true);
            return task.Task;
        }

        private static Task ShowMainWindowAsync() {
            if (AppArguments.Has(AppFlag.ServerManagementMode) && InternalUtils.IsAllRight) {
                return new ModernWindow {
                    Title = "Content Manager Servers Manager",
                    BackButtonVisibility = Visibility.Collapsed,
                    TitleButtonsVisibility = Visibility.Collapsed,
                    MenuTopRowVisibility = Visibility.Collapsed,
                    MenuLinkGroups = {
                        new LinkGroupFilterable {
                            Source = new Uri("/Pages/Lists/ServerPresetsListPage.xaml", UriKind.Relative),
                            GroupKey = "server",
                            DisplayName = AppStrings.Main_ServerPresets,
                            FilterHint = FilterHints.ServerPresets
                        }
                    },
                    DefaultContentSource = new Uri("/Pages/Lists/ServerPresetsListPage.xaml", UriKind.Relative),
                    MinHeight = 400,
                    MinWidth = 800,
                    MaxHeight = DpiAwareWindow.UnlimitedSize,
                    MaxWidth = DpiAwareWindow.UnlimitedSize,
                    Padding = new Thickness(0),
                    SizeToContent = SizeToContent.Manual,
                    ResizeMode = ResizeMode.CanResizeWithGrip,
                    LocationAndSizeKey = @".ServerManagerWindow"
                }.ShowAndWaitAsync();
            }
            if (AppArguments.Has(AppFlag.SimpleQuickDriveMode)) {
                return new ModernWindow {
                    Title = "Content Manager Quick Drive",
                    BackButtonVisibility = Visibility.Collapsed,
                    TitleButtonsVisibility = Visibility.Collapsed,
                    MenuTopRowVisibility = Visibility.Collapsed,
                    IsRootMarginEven = true,
                    MenuLinkGroups = {
                        new LinkGroup {
                            Links = {
                                new Link {
                                    DisplayName = string.Empty,
                                    Source = new Uri("/Pages/Drive/QuickDrive.xaml", UriKind.Relative),
                                }
                            },
                            GroupKey = "drive",
                            DisplayName = string.Empty
                        }
                    },
                    DefaultContentSource = new Uri("/Pages/Drive/QuickDrive.xaml", UriKind.Relative),
                    MinHeight = 400,
                    MinWidth = 600,
                    MaxHeight = DpiAwareWindow.UnlimitedSize,
                    MaxWidth = DpiAwareWindow.UnlimitedSize,
                    Padding = new Thickness(0),
                    SizeToContent = SizeToContent.Manual,
                    ResizeMode = ResizeMode.CanResizeWithGrip,
                    LocationAndSizeKey = @".SimpleQuickDriveWindow"
                }.ShowAndWaitAsync();
            }
            return new MainWindow().ShowAndWaitAsync();
        }

        public void Run(Action mainWindowIsReadyCallback) {
            ((Action)(async () => {
                EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
                ContentInstallationManager.Instance.TaskAdded += OnTaskAdded;

                try {
                    if ((!Superintendent.Instance.IsReady || AcRootDirectorySelector.IsReviewNeeded()) &&
                            new AcRootDirectorySelector().ShowDialog() != true) {
                        Logging.Write("AC root selection cancelled, exit");
                        return;
                    }

                    if (!AppArguments.Values.Any()
                            || await ArgumentsHandler.ProcessArguments(AppArguments.Values, false) != ArgumentsHandler.ShowMainWindow.No) {
                        _showMainWindow = true;
                    }

                    if (_additionalProcessing > 0) {
                        Logging.Write("Waiting for extra workers…");
                        await WaitForInProgress();
                        Logging.Write("Done");
                    }

                    if (_showMainWindow && !_mainWindowShown) {
                        Logging.Write("Main window…");
                        _mainWindowShown = true;
                        mainWindowIsReadyCallback?.Invoke();
                        await (_mainWindowTask ?? ShowMainWindowAsync());
                        Logging.Write("Main window closed");
                    }

                    Logging.Write("Waiting for extra windows to close…");

                    do {
                        await Task.Delay(100);
                    } while (await WaitForWindowToClose(_application.Windows.OfType<DpiAwareWindow>().FirstOrDefault(x => x.IsVisible)));

                    Logging.Write("No more windows");
                } catch (Exception e) {
                    FatalErrorHandler.OnFatalError(e);
                    Logging.Error(e);
                } finally {
                    Logging.Write("Shutdown…");
                    _timer.Stop();
                    _application.Shutdown();
                    System.Windows.Forms.Application.Exit();
                }

                Logging.Write("Main loop is finished");
            })).InvokeInMainThreadAsync();
        }
    }
}