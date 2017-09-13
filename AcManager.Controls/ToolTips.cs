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

        public static CarObject GetCarObject(DependencyObject obj) {
            return (CarObject)obj.GetValue(CarObjectProperty);
        }

        public static void SetCarObject(DependencyObject obj, CarObject value) {
            obj.SetValue(CarObjectProperty, value);
        }

        public static readonly DependencyProperty CarObjectProperty = DependencyProperty.RegisterAttached("CarObject", typeof(CarObject),
                typeof(ToolTips), new UIPropertyMetadata(OnToolTipChanged));

        public static CarSkinObject GetCarSkinObject(DependencyObject obj) {
            return (CarSkinObject)obj.GetValue(CarSkinObjectProperty);
        }

        public static void SetCarSkinObject(DependencyObject obj, CarSkinObject value) {
            obj.SetValue(CarSkinObjectProperty, value);
        }

        public static readonly DependencyProperty CarSkinObjectProperty = DependencyProperty.RegisterAttached("CarSkinObject", typeof(CarSkinObject),
                typeof(ToolTips), new UIPropertyMetadata(OnToolTipChanged));

        private static void OnToolTipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = d as FrameworkElement;
            if (element == null || e.NewValue == null) return;
            element.MouseEnter -= OnElementMouseEnter;
            element.MouseEnter += OnElementMouseEnter;
        }

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

        private static void SetToolTip(FrameworkElement obj, [CanBeNull] ToolTip t, object dataContext) {
            if (t == null) {
                obj.ToolTip = null;
                return;
            }

            t.DataContext = dataContext;
            obj.ToolTip = t;
        }

        private static void OnElementMouseEnter(object sender, MouseEventArgs mouseEventArgs) {
            var element = (FrameworkElement)sender;
            var car = GetCarObject(element);
            if (car != null) {
                SetToolTip(element, GetCarToolTip(element), car);
                return;
            }

            var skin = GetCarSkinObject(element);
            if (skin != null) {
                SetToolTip(element, GetCarSkinToolTip(element), skin);
                return;
            }
        }
    }
}