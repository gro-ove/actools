using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Attached {
    public class FancyHintsService : NotifyPropertyChanged {
        public static readonly FancyHintsService Instance = new FancyHintsService();

        private FancyHintsService() { }

        private bool? _enabled;

        public bool Enabled {
            get => _enabled ?? (_enabled = ValuesStorage.Get("Settings.FancyHintsService.Enabled", true)).Value;
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
            hint.Unnecessary += OnHintUnnecessary;
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

        private static void OnHintUnnecessary(object sender, EventArgs eventArgs) {
            var hint = (FancyHint)sender;
            if (hint != null && FancyHintAdorner.Current?.Hint == hint) {
                FancyHintAdorner.Current.ForceClose();
            }
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
                if (!reference.TryGetTarget(out var f)) return;
                if (GetHint(f) == hint.Id && f.IsLoaded && TryToShow(f, hint)) {
                    eventArgs.Shown = true;
                    return;
                }
            }
        }

        private static readonly List<WeakReference<FrameworkElement>> Elements = new List<WeakReference<FrameworkElement>>(10);

        private static void Purge() {
            foreach (var toDelete in Elements.Where(x => !x.TryGetTarget(out _)).ToList()) {
                Elements.Remove(toDelete);
            }
        }

        public static bool GetHintsDecorator(DependencyObject obj) {
            return obj.GetValue(HintsDecoratorProperty) as bool? == true;
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
            if (!(d is FrameworkElement u)) return;
            Purge();

            var reference = Elements.FirstOrDefault(x => x.TryGetTarget(out var f) && ReferenceEquals(f, u));
            if (GetHint(u) == null) {
                if (reference != null) {
                    Elements.Remove(reference);
                    u.Loaded -= OnElementLoaded;
                }
            } else if (reference == null) {
                Elements.Add(new WeakReference<FrameworkElement>(u));
                if (u.IsLoaded) {
                    OnElementLoaded(u, null);
                } else {
                    u.Loaded += OnElementLoaded;
                }
            }
        }

        private static void OnElementLoaded(object sender, RoutedEventArgs routedEventArgs) {
            if (!(sender is FrameworkElement e) || !GetTriggerOnLoad(e) || !e.IsVisible) return;
            var id = GetHint(e);
            _nextHint = Tuple.Create(id, e);
            Hints.FirstOrDefault(x => x.Id == id)?.Trigger();
        }

        private static Tuple<AdornerLayer, Window> GetAdornerLayer([NotNull] Visual visual) {
            // return null;
            if (visual == null) throw new ArgumentNullException(nameof(visual));

            var window = visual as Window ?? visual.GetParent<Window>();
            var layer = window?.FindVisualChildren<AdornerDecorator>().FirstOrDefault(GetHintsDecorator)?.AdornerLayer;
            return layer != null ? Tuple.Create(layer, window) : null;
        }

        #region Style-related
        public static HorizontalAlignment GetHorizontalAlignment(DependencyObject obj) {
            return obj.GetValue(HorizontalAlignmentProperty) as HorizontalAlignment? ?? default;
        }

        public static void SetHorizontalAlignment(DependencyObject obj, HorizontalAlignment value) {
            obj.SetValue(HorizontalAlignmentProperty, value);
        }

        public static readonly DependencyProperty HorizontalAlignmentProperty = DependencyProperty.RegisterAttached("HorizontalAlignment",
                typeof(HorizontalAlignment),
                typeof(FancyHintsService), new FrameworkPropertyMetadata(HorizontalAlignment.Stretch, FrameworkPropertyMetadataOptions.None));

        public static VerticalAlignment GetVerticalAlignment(DependencyObject obj) {
            return obj.GetValue(VerticalAlignmentProperty) as VerticalAlignment? ?? default;
        }

        public static void SetVerticalAlignment(DependencyObject obj, VerticalAlignment value) {
            obj.SetValue(VerticalAlignmentProperty, value);
        }

        public static readonly DependencyProperty VerticalAlignmentProperty = DependencyProperty.RegisterAttached("VerticalAlignment", typeof(VerticalAlignment),
                typeof(FancyHintsService), new FrameworkPropertyMetadata(VerticalAlignment.Stretch, FrameworkPropertyMetadataOptions.None));

        public static HorizontalAlignment GetHorizontalContentAlignment(DependencyObject obj) {
            return obj.GetValue(HorizontalContentAlignmentProperty) as HorizontalAlignment? ?? default;
        }

        public static void SetHorizontalContentAlignment(DependencyObject obj, HorizontalAlignment value) {
            obj.SetValue(HorizontalContentAlignmentProperty, value);
        }

        public static readonly DependencyProperty HorizontalContentAlignmentProperty = DependencyProperty.RegisterAttached("HorizontalContentAlignment",
                typeof(HorizontalAlignment), typeof(FancyHintsService),
                new FrameworkPropertyMetadata(HorizontalAlignment.Left, FrameworkPropertyMetadataOptions.None));

        public static VerticalAlignment GetVerticalContentAlignment(DependencyObject obj) {
            return obj.GetValue(VerticalContentAlignmentProperty) as VerticalAlignment? ?? default;
        }

        public static void SetVerticalContentAlignment(DependencyObject obj, VerticalAlignment value) {
            obj.SetValue(VerticalContentAlignmentProperty, value);
        }

        public static readonly DependencyProperty VerticalContentAlignmentProperty = DependencyProperty.RegisterAttached("VerticalContentAlignment",
                typeof(VerticalAlignment), typeof(FancyHintsService),
                new FrameworkPropertyMetadata(VerticalAlignment.Top, FrameworkPropertyMetadataOptions.None));

        public static double GetOffsetX(DependencyObject obj) {
            return obj.GetValue(OffsetXProperty) as double? ?? 0d;
        }

        public static void SetOffsetX(DependencyObject obj, double value) {
            obj.SetValue(OffsetXProperty, value);
        }

        public static readonly DependencyProperty OffsetXProperty = DependencyProperty.RegisterAttached("OffsetX", typeof(double),
                typeof(FancyHintsService), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.None));

        public static double GetOffsetY(DependencyObject obj) {
            return obj.GetValue(OffsetYProperty) as double? ?? 0d;
        }

        public static void SetOffsetY(DependencyObject obj, double value) {
            obj.SetValue(OffsetYProperty, value);
        }

        public static readonly DependencyProperty OffsetYProperty = DependencyProperty.RegisterAttached("OffsetY", typeof(double),
                typeof(FancyHintsService), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.None));

        public static bool GetTriggerOnLoad(DependencyObject obj) {
            return obj.GetValue(TriggerOnLoadProperty) as bool? == true;
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