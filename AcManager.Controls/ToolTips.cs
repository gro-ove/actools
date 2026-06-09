using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AcManager.Controls.Helpers;
using AcManager.Controls.Presentation;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace AcManager.Controls {
    public class ToolTips {
        static ToolTips (){
            EventManager.RegisterClassHandler(typeof(ScrollViewer), ScrollViewer.ScrollChangedEvent, new RoutedEventHandler(OnScrollChanged));
        }

        private static ResourceDictionary _dictionary;

        private static ResourceDictionary Dictionary => _dictionary ?? (_dictionary = new SharedResourceDictionary {
            Source = new Uri("/AcManager.Controls;component/Assets/ToolTips.xaml", UriKind.Relative)
        });

        public static CarObject GetCar(DependencyObject obj) {
            return (CarObject)obj.GetValue(CarProperty);
        }

        public static void SetCar(DependencyObject obj, CarObject value) {
            obj.SetValue(CarProperty, value);
        }

        public static readonly DependencyProperty CarProperty = DependencyProperty.RegisterAttached("Car", typeof(CarObject),
                typeof(ToolTips), new UIPropertyMetadata(OnToolTipChanged));

        public static CarSkinObject GetCarSkin(DependencyObject obj) {
            return (CarSkinObject)obj.GetValue(CarSkinProperty);
        }

        public static void SetCarSkin(DependencyObject obj, CarSkinObject value) {
            obj.SetValue(CarSkinProperty, value);
        }

        public static readonly DependencyProperty CarSkinProperty = DependencyProperty.RegisterAttached("CarSkin", typeof(CarSkinObject),
                typeof(ToolTips), new UIPropertyMetadata(OnToolTipChanged));

        public static TrackObjectBase GetTrack(DependencyObject obj) {
            return (TrackObjectBase)obj.GetValue(TrackProperty);
        }

        public static void SetTrack(DependencyObject obj, TrackObjectBase value) {
            obj.SetValue(TrackProperty, value);
        }

        public static readonly DependencyProperty TrackProperty = DependencyProperty.RegisterAttached("Track", typeof(TrackObjectBase),
                typeof(ToolTips), new UIPropertyMetadata(OnToolTipChanged));

        public static WeatherObject GetWeather(DependencyObject obj) {
            return (WeatherObject)obj.GetValue(WeatherProperty);
        }

        public static void SetWeather(DependencyObject obj, WeatherObject value) {
            obj.SetValue(WeatherProperty, value);
        }

        public static readonly DependencyProperty WeatherProperty = DependencyProperty.RegisterAttached("Weather", typeof(WeatherObject),
                typeof(ToolTips), new UIPropertyMetadata(OnToolTipChanged));

        public static ICupSupportedObject GetCupUpdate(DependencyObject obj) {
            return (ICupSupportedObject)obj.GetValue(CupUpdateProperty);
        }

        public static void SetCupUpdate(DependencyObject obj, ICupSupportedObject value) {
            obj.SetValue(CupUpdateProperty, value);
        }

        public static readonly DependencyProperty CupUpdateProperty = DependencyProperty.RegisterAttached("CupUpdate", typeof(ICupSupportedObject),
                typeof(ToolTips), new UIPropertyMetadata(OnToolTipChanged));

        private static void OnToolTipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is FrameworkElement element && e.NewValue != null) {
                ResetToolTip(element);
            }
        }

        private static void ResetToolTip(FrameworkElement element) {
            SetIsDirty(element, true);
            element.MouseEnter -= OnElementMouseEnter;
            element.MouseEnter += OnElementMouseEnter;
            element.PreviewGotKeyboardFocus -= OnElementMouseEnter;
            element.PreviewGotKeyboardFocus += OnElementMouseEnter;

            if (element.IsMouseOver) {
                UpdateToolTip(element);
            }
        }

        public static AcObjectNew GetGenericObject(DependencyObject obj) {
            return (AcObjectNew)obj.GetValue(GenericObjectProperty);
        }

        public static void SetGenericObject(DependencyObject obj, AcObjectNew value) {
            obj.SetValue(GenericObjectProperty, value);
        }

        public static readonly DependencyProperty GenericObjectProperty = DependencyProperty.RegisterAttached("GenericObject", typeof(AcObjectNew),
                typeof(ToolTips), new UIPropertyMetadata(OnGenericObjectChanged));

        private static void OnGenericObjectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            switch (e.NewValue) {
                case CarObject o:
                    SetCar(d, o);
                    break;
                case TrackObjectBase o:
                    SetTrack(d, o);
                    break;
            }
        }

        private static bool GetIsDirty(DependencyObject obj) {
            return obj.GetValue(IsDirtyProperty) as bool? == true;
        }

        private static void SetIsDirty(DependencyObject obj, bool value) {
            obj.SetValue(IsDirtyProperty, value);
        }

        public static readonly DependencyProperty IsDirtyProperty = DependencyProperty.RegisterAttached("IsDirty", typeof(bool),
                typeof(ToolTips), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.None));

        [CanBeNull]
        private static ToolTip GetToolTip(string key, FrameworkElement obj = null) {
            if ((key == @"CarPreviewTooltip" || key == @"TrackPreviewTooltip")
                    && (!AppAppearanceManager.Instance.ShowSelectionDialogToolTips && obj?.GetParent<Window>() is ModernDialog
                    || !AppAppearanceManager.Instance.ShowContentToolTips && obj?.GetParent<AcListPage>() != null)) {
                return null;
            }
            return obj?.TryFindResource(key) as ToolTip ?? Dictionary[key] as ToolTip;
        }

        [CanBeNull]
        public static ToolTip GetCarToolTip(FrameworkElement obj = null) {
            return GetToolTip(@"CarPreviewTooltip", obj);
        }

        [CanBeNull]
        public static ToolTip GetCarSkinToolTip(FrameworkElement obj = null) {
            return GetToolTip(@"CarSkinPreviewTooltip", obj);
        }

        [CanBeNull]
        public static ToolTip GetTrackToolTip(FrameworkElement obj = null) {
            return GetToolTip(@"TrackPreviewTooltip", obj);
        }

        [CanBeNull]
        public static ToolTip GetWeatherToolTip(FrameworkElement obj = null) {
            return GetToolTip(@"WeatherPreviewTooltip", obj);
        }

        [CanBeNull]
        public static ToolTip GetCupUpdateToolTip(FrameworkElement obj = null) {
            if (obj == null) return null;
            return GetToolTip(obj.GetValue(CupUi.InformationModeProperty) as bool? == true ? @"CupInformationTooltip" : @"CupUpdateTooltip", obj);
        }

        private static void SetToolTip(FrameworkElement obj, [CanBeNull] ToolTip t, object dataContext) {
            if (t == null) {
                obj.ToolTip = null;
                return;
            }

            if (obj.ToolTip == null) {
                obj.SetBinding(ToolTipService.IsEnabledProperty, new Binding {
                    Path = new PropertyPath(nameof(ToolTipIsEnabledValue.IsEnabled)),
                    Source = _isIsEnabledValue
                });
            }

            t.DataContext = dataContext;
            obj.ToolTip = t;
        }

        private static ToolTipIsEnabledValue _isIsEnabledValue = new ToolTipIsEnabledValue();

        private class ToolTipIsEnabledValue : NotifyPropertyChanged {
            private bool _isEnabled = true;

            public bool IsEnabled {
                get => _isEnabled;
                set => Apply(value, ref _isEnabled);
            }
        }

        private static long _scrollId;

        private static void OnScrollChanged(object sender, RoutedEventArgs e) {
            var scrollId = ++_scrollId;
            _isIsEnabledValue.IsEnabled = false;
            Task.Delay(300).ContinueWith(t => {
                if (_scrollId != scrollId) return;
                ActionExtension.InvokeInMainThreadAsync(() => _isIsEnabledValue.IsEnabled = true);
            });
        }

        private static void UpdateToolTip(FrameworkElement element) {
            if (!GetIsDirty(element)) return;
            SetIsDirty(element, false);

            var car = GetCar(element);
            if (car != null) {
                SetToolTip(element, GetCarToolTip(element), car);
                return;
            }

            var skin = GetCarSkin(element);
            if (skin != null) {
                SetToolTip(element, GetCarSkinToolTip(element), skin);
                return;
            }

            var track = GetTrack(element);
            if (track != null) {
                SetToolTip(element, GetTrackToolTip(element), track);
                return;
            }

            var weather = GetWeather(element);
            if (weather != null) {
                SetToolTip(element, GetWeatherToolTip(element), weather);
                return;
            }

            var cupUpdate = GetCupUpdate(element);
            if (cupUpdate != null) {
                SetToolTip(element, GetCupUpdateToolTip(element), cupUpdate);
                return;
            }
        }

        private static void OnElementMouseEnter(object sender, EventArgs mouseEventArgs) {
            UpdateToolTip((FrameworkElement)sender);
        }
    }
}