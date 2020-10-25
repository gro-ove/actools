using System.Linq;
using System.Windows;
using System.Windows.Input;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Attached {
    public static class CommandBindingBehavior {
        public static bool GetPropagateToFrame(FrameworkElement obj) {
            return obj.GetValue(PropagateToFrameProperty) as bool? == true;
        }

        public static void SetPropagateToFrame(FrameworkElement obj, bool value) {
            obj.SetValue(PropagateToFrameProperty, value);
        }

        public static readonly DependencyProperty PropagateToFrameProperty =
                DependencyProperty.RegisterAttached("PropagateToFrame", typeof(bool), typeof(CommandBindingBehavior),
                        new PropertyMetadata(false, OnPropagateToFrameChanged));

        private static void OnPropagateToFrameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = (FrameworkElement)d;
            element.Loaded += OnLoaded;
        }

        private static void OnLoaded(object sender, RoutedEventArgs e) {
            var element = (FrameworkElement)sender;
            var parent = element.GetParent<ModernFrame>();
            if (parent == null) return;

            var customBindings = element.CommandBindings.OfType<CommandBinding>()
                    .Where(x => !parent.CommandBindings.Contains(x)).ToList();
            if (customBindings.Count <= 0) return;

            foreach (var binding in customBindings) {
                parent.CommandBindings.Insert(0, binding);
            }
            element.OnActualUnload(() => {
                foreach (var binding in customBindings) {
                    parent.CommandBindings.Remove(binding);
                }
            });
        }
    }
}