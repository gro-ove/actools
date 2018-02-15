using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    [Localizable(false)]
    public static class DependencyObjectHelper {
        [NotNull]
        public static T RequireChild<T>([CanBeNull] this DependencyObject parent, [CanBeNull] string childName) where T : FrameworkElement {
            return parent.FindChild<T>(childName) ?? throw new ArgumentException($"Child with type {typeof(T).Name} named “{childName}” not found");
        }

        [CanBeNull]
        public static T FindChild<T>([CanBeNull] this DependencyObject parent, [CanBeNull] string childName) where T : FrameworkElement {
            if (parent == null) return null;

            var childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < childrenCount; i++) {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t && (string.IsNullOrEmpty(childName) || t.Name == childName)) {
                    return t;
                }

                var foundChild = FindChild<T>(child, childName);
                if (foundChild != null) return foundChild;
            }

            return null;
        }
    }
}