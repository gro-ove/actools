using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Controls {
    public class ToolTips {
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

        private static void OnToolTipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (!(d is FrameworkElement element) || e.NewValue == null) return;

            SetIsDirty(element, true);
            element.MouseEnter -= OnElementMouseEnter;
            element.MouseEnter += OnElementMouseEnter;
            element.PreviewGotKeyboardFocus -= OnElementMouseEnter;
            element.PreviewGotKeyboardFocus += OnElementMouseEnter;

            if (element.IsMouseOver) {
                UpdateContextMenu(element);
            }
        }

        private static bool GetIsDirty(DependencyObject obj) {
            return obj.GetValue(IsDirtyProperty) as bool? == true;
        }

        private static void SetIsDirty(DependencyObject obj, bool value) {
            obj.SetValue(IsDirtyProperty, value);
        }

        public static readonly DependencyProperty IsDirtyProperty = DependencyProperty.RegisterAttached("IsDirty", typeof(bool),
                typeof(ToolTips), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

        [CanBeNull]
        private static ToolTip GetToolTip(string key, FrameworkElement obj = null) {
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

        private static void SetToolTip(FrameworkElement obj, [CanBeNull] ToolTip t, object dataContext) {
            if (t == null) {
                obj.ToolTip = null;
                return;
            }

            t.DataContext = dataContext;
            obj.ToolTip = t;
        }

        private static void UpdateContextMenu(FrameworkElement element) {
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
        }

        private static void OnElementMouseEnter(object sender, EventArgs mouseEventArgs) {
            UpdateContextMenu((FrameworkElement)sender);
        }
    }
}