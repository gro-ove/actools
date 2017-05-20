using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Attached {
    public class ShowHintEventArgs : EventArgs {
        public bool Shown { get; set; }
    }

    public class FancyHint : NotifyPropertyChanged {
        #if DEBUG
        public static TimeSpan OptionMinimumTimeGap = TimeSpan.FromSeconds(10d);
        public static bool OptionDebugMode = true;
        #else
        public static TimeSpan OptionMinimumTimeGap = TimeSpan.FromMinutes(30d);
        public static bool OptionDebugMode = false;
        #endif

        private static readonly string KeyStartup = "__fancyHint:startupId";
        private static readonly int StartupId;

        private static readonly string KeyLastShown = "__fancyHint:lastShown";
        private static DateTime _lastShown;

        static FancyHint() {
            StartupId = ValuesStorage.GetInt(KeyStartup);
            ValuesStorage.Set(KeyStartup, StartupId + 1);

            _lastShown = ValuesStorage.GetDateTime(KeyLastShown, DateTime.MinValue);
        }

        private readonly string _keyShown, _keyAvailable;
        private readonly int _startupsDelay;
        private int _triggersDelay;
        private readonly bool _forced;

        public FancyHint(string id, string header, string description, double probability = 1d, bool availableByDefault = true,
                int startupsDelay = 2, int triggersDelay = 0, bool forced = false) {
            Id = id;
            Header = header;
            Description = description;
            Probability = probability;
            _keyShown = $"__fancyHint:shown:{id}";
            _keyAvailable = availableByDefault ? null : $"__fancyHint:available:{id}";

            _startupsDelay = startupsDelay;
            _triggersDelay = triggersDelay;
            _forced = forced;

            FancyHintsService.Register(this);
        }

        [ThreadStatic]
        private static Random _random;
        public static Random RandomInstance => _random ?? (_random = new Random(Guid.NewGuid().GetHashCode()));

        public string Id { get; }

        public string Header { get; }

        public string Description { get; }

        public double Probability { get; }

        public async void Trigger(TimeSpan delay) {
            await Task.Delay(delay);
            ActionExtension.InvokeInMainThreadAsync(() => {
                if (!OptionDebugMode && Shown) {
                    Logging.Debug($"{Id}: already shown");
                    return;
                }

                if (!Available) {
                    Logging.Debug($"{Id}: not available");
                    return;
                }

                if (!OptionDebugMode && RandomInstance.NextDouble() > Probability) {
                    Logging.Debug($"{Id}: not now, random says so");
                    return;
                }

                if (_startupsDelay > StartupId) {
                    Logging.Debug($"{Id}: waiting for a next start up ({_startupsDelay}, current: {StartupId})");
                    return;
                }

                if (_triggersDelay-- > 0) {
                    Logging.Debug($"{Id}: waiting for a next ({_triggersDelay + 1}) trigger");
                    return;
                }

                if (!_forced && DateTime.Now - _lastShown < OptionMinimumTimeGap) {
                    Logging.Debug($"{Id}: gap ({DateTime.Now - _lastShown}, required: {OptionMinimumTimeGap}) is too low");
                    return;
                }

                var args = new ShowHintEventArgs();
                Show?.Invoke(this, args);
                if (!args.Shown) {
                    Logging.Warning($"{Id}: cancelled!");
                    return;
                }

                Logging.Warning($"{Id}: shown");
                Shown = true;
                _lastShown = DateTime.Now;
                ValuesStorage.Set(KeyLastShown, _lastShown);
            });
        }

        public void Trigger() {
            Trigger(TimeSpan.FromMilliseconds(300));
        }

        public void MarkAsAvailable() {
            Available = true;
        }

        public void MaskAsUnnecessary() {
            Logging.Debug($"{Id}: unnecessary");
            Shown = true;
        }

        private bool? _available;

        public bool Available {
            get { return _keyAvailable == null || (_available ?? (_available = ValuesStorage.GetBool(_keyAvailable)).Value); }
            private set {
                if (_keyAvailable == null || Equals(value, Available)) return;
                _available = value;
                ValuesStorage.Set(_keyAvailable, value);
                OnPropertyChanged();
            }
        }

        private bool? _shown;

        public bool Shown {
            get { return _shown ?? (_shown = ValuesStorage.GetBool(_keyShown)).Value; }
            private set {
                if (Equals(value, Shown)) return;
                _shown = value;
                ValuesStorage.Set(_keyShown, value);
                OnPropertyChanged();
            }
        }

        public event EventHandler<ShowHintEventArgs> Show;
    }

    public class FancyHintsService : NotifyPropertyChanged {
        public static readonly FancyHintsService Instance = new FancyHintsService();

        private FancyHintsService() { }

        private bool? _enabled;

        public bool Enabled {
            get { return _enabled ?? (_enabled = ValuesStorage.GetBool("Settings.FancyHintsService.Enabled", true)).Value; }
            set {
                if (Equals(value, _enabled)) return;
                _enabled = value;
                ValuesStorage.Set("Settings.FancyHintsService.Enabled", value);
                OnPropertyChanged();
            }
        }

        // private static readonly List<FancyHint> Hints = new List<FancyHint>(10);

        internal static void Register(FancyHint hint) {
            // Hints.Add(hint);
            hint.Show += OnHintShow;
        }

        private static void OnHintShow(object sender, ShowHintEventArgs eventArgs) {
            if (!Instance.Enabled) return;

            var hint = (FancyHint)sender;

            Purge();
            foreach (var reference in Elements) {
                FrameworkElement f;
                if (!reference.TryGetTarget(out f)) return;

                if (GetHint(f) == hint.Id && f.IsLoaded) {
                    var layer = GetAdornerLayer(f);
                    if (layer != null) {
                        layer.Add(new FancyHintAdorner(f, layer, hint));
                        eventArgs.Shown = true;
                        return;
                    }
                }
            }
        }

        private static readonly List<WeakReference<FrameworkElement>> Elements = new List<WeakReference<FrameworkElement>>(10);

        private static void Purge() {
            foreach (var toDelete in Elements.Where(x => {
                FrameworkElement f;
                return !x.TryGetTarget(out f);
            }).ToList()) {
                Elements.Remove(toDelete);
            }
        }

        public static bool GetHintsDecorator(DependencyObject obj) {
            return (bool)obj.GetValue(HintsDecoratorProperty);
        }

        public static void SetHintsDecorator(DependencyObject obj, bool value) {
            obj.SetValue(HintsDecoratorProperty, value);
        }

        public static readonly DependencyProperty HintsDecoratorProperty = DependencyProperty.RegisterAttached("HintsDecorator", typeof(bool),
                typeof(FancyHintsService), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.None));

        public static string GetHint(DependencyObject obj) {
            return (string)obj.GetValue(HintProperty);
        }

        public static void SetHint(DependencyObject obj, string value) {
            obj.SetValue(HintProperty, value);
        }

        public static readonly DependencyProperty HintProperty = DependencyProperty.RegisterAttached("Hint", typeof(string),
                typeof(FancyHintsService), new UIPropertyMetadata(OnHintChanged));

        private static void OnHintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var u = d as FrameworkElement;
            if (u == null) return;

            Purge();

            var reference = Elements.FirstOrDefault(x => {
                FrameworkElement f;
                return x.TryGetTarget(out f) && ReferenceEquals(f, u);
            });

            if (GetHint(u) == null) {
                if (reference != null) {
                    Elements.Remove(reference);
                }
            } else if (reference == null){
                Elements.Add(new WeakReference<FrameworkElement>(u));
            }
        }

        private static AdornerLayer GetAdornerLayer(Visual visual) {
            if (visual == null) throw new ArgumentNullException(nameof(visual));

            foreach (var parent in visual.GetParents()) {
                {
                    var decorator = parent as AdornerDecorator;
                    if (decorator != null && GetHintsDecorator(decorator)) return decorator.AdornerLayer;
                }

                {
                    var dialog = parent as Window;
                    if (dialog != null) return dialog.FindVisualChildren<AdornerDecorator>().FirstOrDefault(GetHintsDecorator)?.AdornerLayer;
                }
            }

            return null;
        }

        #region Style-related
        public static HorizontalAlignment GetHorizontalAlignment(DependencyObject obj) {
            return (HorizontalAlignment)obj.GetValue(HorizontalAlignmentProperty);
        }

        public static void SetHorizontalAlignment(DependencyObject obj, HorizontalAlignment value) {
            obj.SetValue(HorizontalAlignmentProperty, value);
        }

        public static readonly DependencyProperty HorizontalAlignmentProperty = DependencyProperty.RegisterAttached("HorizontalAlignment", typeof(HorizontalAlignment),
                typeof(FancyHintsService), new FrameworkPropertyMetadata(HorizontalAlignment.Left, FrameworkPropertyMetadataOptions.None));

        public static VerticalAlignment GetVerticalAlignment(DependencyObject obj) {
            return (VerticalAlignment)obj.GetValue(VerticalAlignmentProperty);
        }

        public static void SetVerticalAlignment(DependencyObject obj, VerticalAlignment value) {
            obj.SetValue(VerticalAlignmentProperty, value);
        }

        public static readonly DependencyProperty VerticalAlignmentProperty = DependencyProperty.RegisterAttached("VerticalAlignment", typeof(VerticalAlignment),
                typeof(FancyHintsService), new FrameworkPropertyMetadata(VerticalAlignment.Top, FrameworkPropertyMetadataOptions.None));

        public static double GetOffsetX(DependencyObject obj) {
            return (double)obj.GetValue(OffsetXProperty);
        }

        public static void SetOffsetX(DependencyObject obj, double value) {
            obj.SetValue(OffsetXProperty, value);
        }

        public static readonly DependencyProperty OffsetXProperty = DependencyProperty.RegisterAttached("OffsetX", typeof(double),
                typeof(FancyHintsService), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.None));

        public static double GetOffsetY(DependencyObject obj) {
            return (double)obj.GetValue(OffsetYProperty);
        }

        public static void SetOffsetY(DependencyObject obj, double value) {
            obj.SetValue(OffsetYProperty, value);
        }

        public static readonly DependencyProperty OffsetYProperty = DependencyProperty.RegisterAttached("OffsetY", typeof(double),
                typeof(FancyHintsService), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.None));
        #endregion
    }
}