using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using JetBrains.Annotations;

namespace AcManager.Controls.Presentation {
    internal static class DependencyObjectExtension {
        public static IEnumerable<T> FindVisualChildren<T>([NotNull] this DependencyObject depObj) where T : DependencyObject {
            if (depObj == null) throw new ArgumentNullException(nameof(depObj));

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++) {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T children) {
                    yield return children;
                }

                foreach (var childOfChild in FindVisualChildren<T>(child)) {
                    yield return childOfChild;
                }
            }
        }

        public static T Seal<T>([NotNull] this T depObj) where T : Freezable {
            depObj.Freeze();
            return depObj;
        }
    }
}