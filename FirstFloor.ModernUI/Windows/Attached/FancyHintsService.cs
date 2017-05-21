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
using JetBrains.Annotations;

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
                int startupsDelay = 2, int triggersDelay = 0, bool forced = false, bool closeOnResize = false) {
            Id = id;
            Header = header;
            Description = description;
            Probability = probability;
            CloseOnResize = closeOnResize;
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
        public bool CloseOnResize { get; }

        public async void Trigger(TimeSpan delay) {
            await Task.Delay(delay);
            ActionExtension.InvokeInMainThreadAsync(() => {
                if (FancyHintAdorner.IsAnyShown) {
                    Logging.Debug($"{Id}: something else is being shown right now");
                    return;
                }

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

        private static readonly List<FancyHint> Hints = new List<FancyHint>(10);

        internal static void Register(FancyHint hint) {
            Hints.Add(hint);
            hint.Show += OnHintShow;
        }

        private static Tuple<string, FrameworkElement> _nextHint;

        private static bool TryToShow(FrameworkElement element, FancyHint hint) {
            var parent = element;

            var attachTo = GetAttachTo(element);
            if (attachTo != null) {
                var attachToType = attachTo as Type;
                if (attachToType != null) {
                    element = element.FindVisualChildren<FrameworkElement>().FirstOrDefault(x => {
                        var t = x.GetType();
                        return t == attachToType || t.IsSubclassOf(attachToType);
                    });
                } else {
                    // TODO
                    element = null;
                }
            }

            if (element == null) {
                Logging.Warning("Element is null!");
                return false;
            }

            var layer = GetAdornerLayer(element);
            if (layer == null) {
                Logging.Warning("Can’t find adorner layer!");
                return false;
            }

            layer.Item1.Add(new FancyHintAdorner(element, parent, layer.Item1, layer.Item2, hint));
            return true;
        }

        private static void OnHintShow(object sender, ShowHintEventArgs eventArgs) {
            if (!Instance.Enabled) return;

            var hint = (FancyHint)sender;

            if (_nextHint != null) {
                var next = _nextHint;
                _nextHint = null;

                if (next.Item1 == hint.Id && TryToShow(next.Item2, hint)) {
                    eventArgs.Shown = true;
                    return;
                }
            }

            Purge();
            foreach (var reference in Elements) {
                FrameworkElement f;
                if (!reference.TryGetTarget(out f)) return;

                if (GetHint(f) == hint.Id && f.IsLoaded && TryToShow(f, hint)) {
                    eventArgs.Shown = true;
                    return;
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
                    u.Loaded -= OnElementLoaded;
                }
            } else if (reference == null){
                Elements.Add(new WeakReference<FrameworkElement>(u));
                if (u.IsLoaded) {
                    OnElementLoaded(u, null);
                } else {
                    u.Loaded += OnElementLoaded;
                }
            }
        }

        private static void OnElementLoaded(object sender, RoutedEventArgs routedEventArgs) {
            var e = sender as FrameworkElement;
            if (e == null || !GetTriggerOnLoad(e)) return;

            var id = GetHint(e);
            _nextHint = Tuple.Create(id, e);
            Hints.FirstOrDefault(x => x.Id == id)?.Trigger();
        }

        private static Tuple<AdornerLayer, Window> GetAdornerLayer([NotNull] Visual visual) {
            if (visual == null) throw new ArgumentNullException(nameof(visual));

            var window = visual.GetParent<Window>();
            var layer = window?.FindVisualChildren<AdornerDecorator>().FirstOrDefault(GetHintsDecorator)?.AdornerLayer;
            return layer != null ? Tuple.Create(layer, window) : null;
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

        public static bool GetTriggerOnLoad(DependencyObject obj) {
            return (bool)obj.GetValue(TriggerOnLoadProperty);
        }

        public static void SetTriggerOnLoad(DependencyObject obj, bool value) {
            obj.SetValue(TriggerOnLoadProperty, value);
        }

        public static readonly DependencyProperty TriggerOnLoadProperty = DependencyProperty.RegisterAttached("TriggerOnLoad", typeof(bool),
                typeof(FancyHintsService), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.None));

        public static object GetAttachTo(DependencyObject obj) {
            return obj.GetValue(AttachToProperty);
        }

        public static void SetAttachTo(DependencyObject obj, object value) {
            obj.SetValue(AttachToProperty, value);
        }

        public static readonly DependencyProperty AttachToProperty = DependencyProperty.RegisterAttached("AttachTo", typeof(object),
                typeof(FancyHintsService), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

        #endregion
    }
}