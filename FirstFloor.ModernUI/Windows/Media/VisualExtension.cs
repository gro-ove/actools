using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Media {
    public static partial class VisualExtension {
        private static readonly List<Type> InputTypes = new List<Type>();

        public static void RegisterInput<T>() where T : FrameworkElement {
            InputTypes.Add(typeof(T));
        }

        [Pure]
        private static IEnumerable<FrameworkElement> FindVisualChildren([NotNull] this DependencyObject depObj) {
            if (depObj == null) throw new ArgumentNullException(nameof(depObj));

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++) {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is FrameworkElement fe && InputTypes.Contains(fe.GetType())) {
                    yield return fe;
                }

                foreach (var childOfChild in FindVisualChildren(child)) {
                    yield return childOfChild;
                }
            }
        }

        public static bool IsInputFocused() {
            return Keyboard.FocusedElement is TextBoxBase || Keyboard.FocusedElement is PasswordBox || Keyboard.FocusedElement is ComboBox
                    || Application.Current?.Windows.OfType<Window>().SelectMany(FindVisualChildren)
                                  .Any(x => x.IsKeyboardFocused || x.IsKeyboardFocusWithin) == true;
        }
    }
}