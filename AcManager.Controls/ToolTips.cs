using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Presentation;

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

        private static void OnToolTipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = d as FrameworkElement;
            if (element == null || e.NewValue == null) return;
            element.MouseEnter -= OnElementMouseEnter;
            element.MouseEnter += OnElementMouseEnter;
        }

        private static void SetToolTip(FrameworkElement obj, string key, object dataContext) {
            var t = obj.TryFindResource(key) as ToolTip ?? Dictionary[key] as ToolTip;
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
                SetToolTip(element, @"CarPreviewTooltip", car);
            }
        }
    }
}