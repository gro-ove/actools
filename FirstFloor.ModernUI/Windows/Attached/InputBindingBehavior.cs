using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using FirstFloor.ModernUI.Commands;

namespace FirstFloor.ModernUI.Windows.Attached {
    public static class InputBindingBehavior {
        public static bool GetPropagateToWindow(FrameworkElement obj) {
            return obj.GetValue(PropagateToWindowProperty) as bool? == true;
        }

        public static void SetPropagateToWindow(FrameworkElement obj, bool value) {
            obj.SetValue(PropagateToWindowProperty, value);
        }

        public static readonly DependencyProperty PropagateToWindowProperty =
            DependencyProperty.RegisterAttached("PropagateToWindow", typeof(bool), typeof(InputBindingBehavior),
            new PropertyMetadata(false, OnPropagateToWindowChanged));

        private static void OnPropagateToWindowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = (FrameworkElement)d;
            element.Loaded += OnLoaded;
            element.Unloaded += OnUnloaded;
        }

        public static void UpdateBindings(FrameworkElement obj) {
            if (obj.IsLoaded && GetPropagateToWindow(obj)) {
                RemoveBindings(obj);
                SetBindings(obj);
            }
        }

        public static Window GetWindow(DependencyObject obj) {
            return (Window)obj.GetValue(WindowProperty);
        }

        public static void SetWindow(DependencyObject obj, Window value) {
            obj.SetValue(WindowProperty, value);
        }

        public static readonly DependencyProperty WindowProperty = DependencyProperty.RegisterAttached("Window", typeof(Window),
                typeof(InputBindingBehavior), new UIPropertyMetadata(null));

        private static void SetBindings(UIElement element) {
            RemoveBindings(element);

            var window = Window.GetWindow(element);
            if (window == null) return;

            SetWindow(element, window);

            for (var i = element.InputBindings.Count - 1; i >= 0; i--) {
                var binding = element.InputBindings[i];
                binding.CommandTarget = element;
                window.InputBindings.Add(new ExtBinding(window, binding));
                element.InputBindings.RemoveAt(i);
            }
        }

        private class ExtBinding : InputBinding {
            internal readonly InputBinding BaseBinding;

            public ExtBinding(Window window, InputBinding baseBinding) : base(new DelegateCommand<object>(o => {
                if ((baseBinding.Gesture as KeyGesture)?.Modifiers.HasFlag(ModifierKeys.Alt) == true) {
                    var focused = FocusManager.GetFocusedElement(window);
                    if (focused is TextBoxBase || focused is PasswordBox) return;
                }

                baseBinding.Command.Execute(baseBinding.CommandParameter);
            }), baseBinding.Gesture) {
                BaseBinding = baseBinding;
                CommandTarget = baseBinding.CommandTarget;
            }
        }

        private static void RemoveBindings(UIElement element) {
            var window = GetWindow(element);
            if (window == null) return;

            element.ClearValue(WindowProperty);

            for (var i = window.InputBindings.Count - 1; i >= 0; i--) {
                var binding = window.InputBindings[i];
                if (ReferenceEquals(binding.CommandTarget, element)) {
                    window.InputBindings.RemoveAt(i);
                    element.InputBindings.Add(binding is ExtBinding ext ? ext.BaseBinding : binding);
                }
            }
        }

        private static void OnUnloaded(object sender, RoutedEventArgs e) {
            RemoveBindings((FrameworkElement)sender);
        }

        private static void OnLoaded(object sender, RoutedEventArgs e) {
            SetBindings((FrameworkElement)sender);
        }
    }
}
