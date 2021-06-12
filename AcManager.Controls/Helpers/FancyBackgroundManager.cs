using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Controls.Helpers {
    public interface IFancyBackgroundListener {
        void ChangeBackground(string filename);
    }

    public class FancyBackgroundManager : NotifyPropertyChanged {
        public const string KeyEnabled = "FancyBackgroundManager.KeyEnabled";
        public const string KeyBackground = "FancyBackgroundManager.KeyBackground";

        public static FancyBackgroundManager Instance { get; private set; }

        public static FancyBackgroundManager Initialize() {
            if (Instance != null) throw new Exception(@"Already initialized");
            return Instance = new FancyBackgroundManager();
        }

        private FancyBackgroundManager() {
            _listener = new List<IFancyBackgroundListener>();
            _enabled = ValuesStorage.Get(KeyEnabled, false);
            _backgroundFilename = ValuesStorage.Get<string>(KeyBackground);
        }

        private readonly List<IFancyBackgroundListener> _listener;

        public void AddListener(IFancyBackgroundListener listener) {
            _listener.Add(listener);
            if (Enabled) {
                UpdateBackgroundLater(listener).Ignore();
            }
        }

        public void Recreate(IFancyBackgroundListener listener) {
            if (Enabled) {
                UpdateBackgroundLater(listener).Ignore();
            }
        }

        private async Task UpdateBackgroundLater(IFancyBackgroundListener listener) {
            await Task.Delay(100);
            listener.ChangeBackground(Enabled ? BackgroundFilename : null);
        }

        public void RemoveListener(IFancyBackgroundListener listener) {
            _listener.Remove(listener);
        }

        private void Dispatch() {
            foreach (var listener in _listener) {
                UpdateBackgroundLater(listener).Ignore();
            }
        }

        private bool _enabled;

        public bool Enabled {
            get => _enabled;
            set {
                if (Equals(value, _enabled)) return;
                _enabled = value;
                OnPropertyChanged();

                ValuesStorage.Set(KeyEnabled, value);
                Dispatch();
            }
        }

        private string _backgroundFilename;
        private readonly string _cachedFilename = FilesStorage.Instance.GetTemporaryFilename("Background.jpg");

        public string BackgroundFilename {
            get => _backgroundFilename;
            private set {
                if (Equals(value, _backgroundFilename)) return;

                if (File.Exists(value)) {
                    try {
                        if (File.Exists(_cachedFilename)) {
                            File.Delete(_cachedFilename);
                        }

                        File.Copy(value, _cachedFilename);
                        value = _cachedFilename;
                    } catch (Exception e) {
                        Logging.Warning($"Can’t copy background image '{value ?? @"NULL"}': {e}");
                    }
                } else {
                    Logging.Warning($"Background image '{value ?? @"NULL"}' is missing");
                    value = null;
                }

                _backgroundFilename = value;
                OnPropertyChanged();

                ValuesStorage.Set(KeyBackground, _backgroundFilename);
                if (Enabled) {
                    Dispatch();
                }
            }
        }

        private readonly Queue<string> _queue = new Queue<string>();
        private DispatcherTimer _timer;
        private bool? _hasAnything;

        // TODO: Rework behavior
        public void ChangeBackground([CanBeNull] string filename) {
            if (!_hasAnything.HasValue) {
                _hasAnything = File.Exists(_cachedFilename);
            }

            if (!Enabled && _hasAnything.Value) return;

            if (filename == null) return;
            _queue.Enqueue(filename);

            if (_timer != null) return;
            _timer = new DispatcherTimer(DispatcherPriority.Background) {
                Interval = TimeSpan.FromSeconds(1),
                IsEnabled = true
            };
            _timer.Tick += Timer_Tick;
        }

        private void Timer_Tick(object sender, EventArgs e) {
            if (!_queue.Any() || MathUtils.Random() > 0.8) return;

            while (_queue.Count > 2) {
                _queue.Dequeue();
            }

            var filename = _queue.Dequeue();
            if (File.Exists(filename)) {
                BackgroundFilename = filename;
            }
        }

        public static void UpdateBackground(FrameworkElement anyElement, ref object backgroundContent) {
            if (!Instance.Enabled) {
                backgroundContent = ((FrameworkElement)backgroundContent)?.Tag ?? backgroundContent;
                return;
            }

            var rectangle = backgroundContent as Rectangle;
            var visualBrush = rectangle?.Fill as VisualBrush;

            if (visualBrush == null) {
                visualBrush = (VisualBrush)anyElement.FindResource(@"FancyBackgroundBrush");
                rectangle = new Rectangle {
                    Fill = visualBrush,
                    Tag = backgroundContent
                };

                backgroundContent = rectangle;
            }

            var frameworkElement = (FrameworkElement)visualBrush.Visual;
            var backgroundImage0 = (BetterImage)LogicalTreeHelper.FindLogicalNode(frameworkElement, @"BackgroundImage0");
            var backgroundImage1 = (BetterImage)LogicalTreeHelper.FindLogicalNode(frameworkElement, @"BackgroundImage1");
            if (backgroundImage0 == null || backgroundImage1 == null) return;

            var state = frameworkElement.Tag as int? ?? 0;
            (state == 0 ? backgroundImage1 : backgroundImage0).Filename = Instance.BackgroundFilename;
            VisualStateManager.GoToElementState(backgroundImage1, @"State" + state, true);
            frameworkElement.Tag = 1 - state;
        }
    }
}
