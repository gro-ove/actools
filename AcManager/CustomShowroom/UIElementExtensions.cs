using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AcManager.CustomShowroom {
    /// <summary>
    /// Extensions for <see cref="UIElement"/>.
    /// </summary>
    public static class UIElementExtensions {
        #region Methods
        public static DependencyObject FindVisualDescendant(this DependencyObject startElement, Predicate<object> condition) {
            if (startElement != null) {
                if (condition(startElement)) {
                    return startElement;
                }

                var startElementAsUserControl = startElement as UserControl;
                if (startElementAsUserControl != null) {
                    return FindVisualDescendant(startElementAsUserControl.Content as DependencyObject, condition);
                }

                var startElementAsContentControl = startElement as ContentControl;
                if (startElementAsContentControl != null) {
                    return FindVisualDescendant(startElementAsContentControl.Content as DependencyObject, condition);
                }

                var startElementAsBorder = startElement as Border;
                if (startElementAsBorder != null) {
                    return FindVisualDescendant(startElementAsBorder.Child, condition);
                }

#if NET || NETCORE
                var startElementAsDecorator = startElement as Decorator;
                if (startElementAsDecorator != null)
                {
                    return FindVisualDescendant(startElementAsDecorator.Child, condition);
                }
#endif

                // If the element has children, loop the children
                var children = new List<DependencyObject>();

                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(startElement); i++) {
                    children.Add(VisualTreeHelper.GetChild(startElement, i));
                }

                // First, loop children itself
                foreach (var child in children) {
                    if (condition(child)) {
                        return child;
                    }
                }

                // Direct child is not what we are looking for, continue
                foreach (var child in children) {
                    var obj = FindVisualDescendant(child, condition);
                    if (obj != null) {
                        return obj;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the focused control.
        /// </summary>
        /// <param name="element">The element to check and all childs.</param>
        /// <returns>The focused <see cref="UIElement"/> or <c>null</c> if none if the children has the focus.</returns>
        /// <exception cref="ArgumentNullException">The <paramref name="element"/> is <c>null</c>.</exception>
        public static UIElement GetFocusedControl(this UIElement element) {
            return element.FindVisualDescendant(obj => {
                var objAsUIElement = obj as UIElement;
                if (objAsUIElement != null) {
                    return objAsUIElement.IsFocused;
                }

                return false;
            }) as UIElement;
        }

        /// <summary>
        /// Focuses the first control on the ContentElement.
        /// </summary>
        /// <param name="element">Reference to the current <see cref="ContentElement"/>.</param>
        /// <param name="focusParentsFirst">if set to <c>true</c>, the parents are focused first.</param>
        public static void FocusFirstControl(this ContentElement element, bool focusParentsFirst = true) {
            FocusFirstControl((object)element, focusParentsFirst);
        }

        /// <summary>
        /// Focuses the first control on the UI Element.
        /// </summary>
        /// <param name="element">Reference to the current <see cref="UIElement"/>.</param>
        /// <param name="focusParentsFirst">if set to <c>true</c>, the parents are focused first.</param>
        public static void FocusFirstControl(this UIElement element, bool focusParentsFirst = true) {
            FocusFirstControl((object)element, focusParentsFirst);
        }

        /// <summary>
        /// Focuses the first control on the UI Element.
        /// </summary>
        /// <param name="element">Reference to the current element.</param>
        /// <param name="focusParentsFirst">if set to <c>true</c>, the parents are focused first.</param>
        private static void FocusFirstControl(object element, bool focusParentsFirst) {
            var elementAsFrameworkElement = element as FrameworkElement;
            if (elementAsFrameworkElement != null) {
                if (elementAsFrameworkElement.IsLoaded) {
                    FocusNextControl(elementAsFrameworkElement, focusParentsFirst);
                } else {
                    // Get handler (so we can nicely unsubscribe)
                    RoutedEventHandler onFrameworkElementLoaded = null;
                    onFrameworkElementLoaded = delegate {
                        FocusNextControl(elementAsFrameworkElement, focusParentsFirst);
                        elementAsFrameworkElement.Loaded -= onFrameworkElementLoaded;
                    };

                    elementAsFrameworkElement.Loaded += onFrameworkElementLoaded;
                }
            } else {
                FocusFirstControl(element, focusParentsFirst);
            }
        }

        /// <summary>
        /// Focuses the next control on the UI Element.
        /// </summary>
        /// <param name="element">Element to focus the next control of.</param>
        /// <param name="focusParentsFirst">if set to <c>true</c>, the parents are focused first.</param>
        private static void FocusNextControl(object element, bool focusParentsFirst) {
            var elementAsFrameworkElement = element as FrameworkElement;
            if (elementAsFrameworkElement != null) {
                if (focusParentsFirst) {
                    var parentsToFocus = new Stack<FrameworkElement>();
                    var parent = elementAsFrameworkElement.Parent as FrameworkElement;
                    while (parent != null) {
                        if (parent.Focusable) {
                            parentsToFocus.Push(parent);
                        }

                        parent = parent.Parent as FrameworkElement;
                    }

                    while (parentsToFocus.Count > 0) {
                        var parentToFocus = parentsToFocus.Pop();
                        parentToFocus.Focus();
                    }
                }
            }

            var uiElement = element as UIElement;
            var contentElement = element as ContentElement;

            if (uiElement != null) {
                // Focus element itself
                if (uiElement.Focusable) {
                    uiElement.Focus();
                }
            } else if (contentElement != null) {
                // Focus content element
                if (contentElement.Focusable) {
                    contentElement.Focus();
                }
            }

            MoveFocus(element, FocusNavigationDirection.Next, 1);
        }

        /// <summary>
        /// Moves the focus in a specific direction.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="hops">The hops.</param>
        public static void MoveFocus(this IInputElement element, FocusNavigationDirection direction, int hops) {
            MoveFocus((object)element, direction, hops);
        }

        /// <summary>
        /// Moves the focus in a specific direction.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="hops">The hops.</param>
        public static void MoveFocus(this UIElement element, FocusNavigationDirection direction, int hops) {
            MoveFocus((object)element, direction, hops);
        }

        /// <summary>
        /// Moves the focus in a specific direction.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="hops">The hops.</param>
        public static void MoveFocus(this ContentElement element, FocusNavigationDirection direction, int hops) {
            MoveFocus((object)element, direction, hops);
        }

        /// <summary>
        /// Moves the focus in a specific direction.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="hops">The hops.</param>
        private static void MoveFocus(object element, FocusNavigationDirection direction, int hops) {
            if (hops <= 0) {
                return;
            }

            var frameworkElement = element as FrameworkElement;
            bool delayMove = ((frameworkElement != null) && !frameworkElement.IsLoaded);

            if (delayMove) {
                RoutedEventHandler onFrameworkElementLoaded = null;
                onFrameworkElementLoaded = delegate {
                    MoveFocus((object)frameworkElement, direction, hops);
                    frameworkElement.Loaded -= onFrameworkElementLoaded;
                };

                frameworkElement.Loaded += onFrameworkElementLoaded;
            } else {
                var uiElement = element as UIElement;
                var contentElement = element as ContentElement;

                if (uiElement != null) {
                    // Focus next
                    uiElement.MoveFocus(new TraversalRequest(direction));
                } else if (contentElement != null) {
                    // Focus next
                    contentElement.MoveFocus(new TraversalRequest(direction));
                }

                if (hops > 1) {
                    MoveFocus(Keyboard.FocusedElement, direction, hops - 1);
                }
            }
        }
        #endregion
    }
}