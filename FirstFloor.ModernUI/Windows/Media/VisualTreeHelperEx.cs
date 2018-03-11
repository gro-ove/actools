using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Media {
    /// <summary>
    /// Provides addition visual tree helper methods.
    /// </summary>
    public static class VisualTreeHelperEx {
        public static IEnumerable<T> GetAllOfType<T>() where T : DependencyObject {
            var app = Application.Current;
            if (app == null) yield break;
            foreach (var image in app.Windows.OfType<Window>().SelectMany(FindVisualChildren<T>)) {
                yield return image;
            }
        }

        [Pure]
        public static bool IsUserVisible(this FrameworkElement element, FrameworkElement container) {
            if (element.IsVisible != true) return false;
            var bounds = element.TransformToAncestor(container).TransformBounds(new Rect(0.0, 0.0, element.ActualWidth, element.ActualHeight));
            var rect = new Rect(0.0, 0.0, container.ActualWidth, container.ActualHeight);
            return rect.Contains(bounds.TopLeft) || rect.Contains(bounds.BottomRight);
        }

        [Pure]
        public static IEnumerable<T> GetVisibleItemsFromListbox<T>(this ItemsControl listBox) {
            var any = false;
            foreach (var item in listBox.Items.OfType<T>()) {
                var container = listBox.ItemContainerGenerator.ContainerFromItem(item);
                if (container != null && IsUserVisible((ListBoxItem)container, listBox)) {
                    any = true;
                    yield return item;
                } else if (any) {
                    break;
                }
            }
        }

        [Pure]
        public static IEnumerable<T> FindVisualChildren<T>([NotNull] this DependencyObject depObj) where T : DependencyObject {
            if (depObj == null) throw new ArgumentNullException(nameof(depObj));

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++) {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T childT) {
                    yield return childT;
                }

                foreach (var childOfChild in FindVisualChildren<T>(child)) {
                    yield return childOfChild;
                }
            }
        }

        [Pure, CanBeNull]
        public static T FindVisualChild<T>([NotNull] this DependencyObject obj) where T : DependencyObject {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            return FindVisualChildren<T>(obj).FirstOrDefault();
        }

        [Pure]
        public static IEnumerable<T> FindLogicalChildren<T>([NotNull] this DependencyObject depObj) where T : DependencyObject {
            if (depObj == null) throw new ArgumentNullException(nameof(depObj));

            foreach (var child in LogicalTreeHelper.GetChildren(depObj).OfType<DependencyObject>()) {
                if (child is T childT) {
                    yield return childT;
                }

                foreach (var childOfChild in FindLogicalChildren<T>(child)) {
                    yield return childOfChild;
                }
            }
        }

        [Pure, CanBeNull]
        public static T FindLogicalChild<T>([NotNull] this DependencyObject obj) where T : DependencyObject {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            return FindLogicalChildren<T>(obj).FirstOrDefault();
        }

        /// <summary>
        /// Gets specified visual state group.
        /// </summary>
        /// <param name="dependencyObject">The dependency object.</param>
        /// <param name="groupName">Name of the group.</param>
        /// <returns></returns>
        public static VisualStateGroup TryGetVisualStateGroup(this DependencyObject dependencyObject, string groupName) {
            var root = GetImplementationRoot(dependencyObject);
            if (root == null) return null;
            return (from @group in VisualStateManager.GetVisualStateGroups(root)?.OfType<VisualStateGroup>()
                    where string.CompareOrdinal(groupName, @group.Name) == 0
                    select @group).FirstOrDefault();
        }

        /// <summary>
        /// Gets the implementation root.
        /// </summary>
        /// <param name="dependencyObject">The dependency object.</param>
        /// <returns></returns>
        public static FrameworkElement GetImplementationRoot(this DependencyObject dependencyObject) {
            if (1 != VisualTreeHelper.GetChildrenCount(dependencyObject)) {
                return null;
            }
            return VisualTreeHelper.GetChild(dependencyObject, 0) as FrameworkElement;
        }

        /// <summary>
        /// Returns a collection of the visual ancestor elements of specified dependency object.
        /// </summary>
        /// <param name="dependencyObject">The dependency object.</param>
        /// <returns>
        /// A collection that contains the ancestors elements.
        /// </returns>
        public static IEnumerable<DependencyObject> Ancestors(this DependencyObject dependencyObject) {
            var parent = dependencyObject;
            while (true) {
                parent = GetParent(parent);
                if (parent != null) {
                    yield return parent;
                } else {
                    break;
                }
            }
        }

        /// <summary>
        /// Returns a collection of visual elements that contain specified object, and the ancestors of specified object.
        /// </summary>
        /// <param name="dependencyObject">The dependency object.</param>
        /// <returns>
        /// A collection that contains the ancestors elements and the object itself.
        /// </returns>
        public static IEnumerable<DependencyObject> AncestorsAndSelf(this DependencyObject dependencyObject) {
            if (dependencyObject == null) throw new ArgumentNullException(nameof(dependencyObject));

            var parent = dependencyObject;
            while (true) {
                if (parent != null) {
                    yield return parent;
                } else {
                    break;
                }
                parent = GetParent(parent);
            }
        }

        /// <summary>
        /// Gets the parent for specified dependency object.
        /// </summary>
        /// <param name="dependencyObject">The dependency object</param>
        /// <returns>The parent object or null if there is no parent.</returns>
        [Pure, CanBeNull]
        public static DependencyObject GetParent(this DependencyObject dependencyObject) {
            if (dependencyObject == null) throw new ArgumentNullException(nameof(dependencyObject));

            var fe = dependencyObject as FrameworkElement;
            if (fe?.Parent != null) return fe.Parent;

            if (dependencyObject is ContentElement ce) {
                var parent = ContentOperations.GetParent(ce);
                if (parent != null) {
                    return parent;
                }

                var fce = ce as FrameworkContentElement;
                return fce?.Parent;
            }

            return VisualTreeHelper.GetParent(dependencyObject);
        }

        [Pure]
        public static IEnumerable<DependencyObject> GetParents([NotNull] this DependencyObject dependencyObject) {
            if (dependencyObject == null) throw new ArgumentNullException(nameof(dependencyObject));

            var parent = dependencyObject.GetParent();
            while (parent != null) {
                yield return parent;
                parent = parent.GetParent();
            }
        }

        [Pure, CanBeNull]
        public static DependencyObject GetParentWhere([NotNull] this DependencyObject dependencyObject, [NotNull] Func<DependencyObject, bool> predicate) {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            return dependencyObject.GetParents().Where(predicate).FirstOrDefault();
        }

        [Pure, CanBeNull]
        public static T GetParent<T>([NotNull] this DependencyObject dependencyObject) where T : DependencyObject {
            return dependencyObject.GetParents().OfType<T>().FirstOrDefault();
        }

        [Pure, CanBeNull]
        public static T GetFromPoint<T>(this UIElement reference, Point point) where T : DependencyObject {
            return reference.InputHitTest(point) is DependencyObject element ? element as T ?? GetParent<T>(element) : null;
        }

        /// <summary>
        /// Works faster if value the same — for example, usual VerifyAccess() won’t be called.
        /// </summary>
        /// <param name="element">Element.</param>
        /// <param name="visible">Visible or collapsed.</param>
        public static void SetVisibility([CanBeNull] this UIElement element, bool visible) {
            SetVisibility(element, visible ? Visibility.Visible : Visibility.Collapsed);
        }

        /// <summary>
        /// Works faster if value the same — for example, usual VerifyAccess() won’t be called.
        /// </summary>
        /// <param name="element">Element.</param>
        /// <param name="visibility">Visibility.</param>
        public static void SetVisibility([CanBeNull] this UIElement element, Visibility visibility) {
            if (element == null) return;
            if (element.Visibility != visibility) {
                element.Visibility = visibility;
            }
        }

        public static void ResetElementNameBindings([NotNull] this DependencyObject obj) {
            var boundProperties = obj.GetDataBoundProperties();
            foreach (DependencyProperty dp in boundProperties) {
                var binding = BindingOperations.GetBinding(obj, dp);

                //binding itself should never be null, but anyway
                if (!string.IsNullOrEmpty(binding?.ElementName)) {
                    //just updating source and/or target doesn’t do the trick – reset the binding
                    BindingOperations.ClearBinding(obj, dp);
                    BindingOperations.SetBinding(obj, dp, binding);
                }
            }

            var count = VisualTreeHelper.GetChildrenCount(obj);
            for (var i = 0; i < count; i++) {
                //process child items recursively
                var childObject = VisualTreeHelper.GetChild(obj, i);
                ResetElementNameBindings(childObject);
            }
        }

        public static IEnumerable GetDataBoundProperties(this DependencyObject element) {
            var lve = element.GetLocalValueEnumerator();
            while (lve.MoveNext()) {
                var entry = lve.Current;
                if (BindingOperations.IsDataBound(element, entry.Property)) {
                    yield return entry.Property;
                }
            }
        }
    }
}
