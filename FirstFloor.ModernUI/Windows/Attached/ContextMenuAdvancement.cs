using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Windows.Attached {
    public static class ContextMenuAdvancement {
        internal static readonly List<Tuple<ContextMenu, object>> ParentContextMenu = new List<Tuple<ContextMenu, object>>();
        private static DateTime _lastClicked;

        internal static void CheckTime() {
            if (ParentContextMenu.Count > 0 && DateTime.Now - _lastClicked > TimeSpan.FromMilliseconds(500)) {
                ParentContextMenu.Clear();
            }
        }

        public static bool GetAddCopyLinkItem(DependencyObject obj) {
            return (bool)obj.GetValue(AddCopyLinkItemProperty);
        }

        public static void SetAddCopyLinkItem(DependencyObject obj, bool value) {
            obj.SetValue(AddCopyLinkItemProperty, value);
        }

        public static readonly DependencyProperty AddCopyLinkItemProperty = DependencyProperty.RegisterAttached("AddCopyLinkItem", typeof(bool),
                typeof(ContextMenuAdvancement), new UIPropertyMetadata(OnAddCopyLinkItemChanged));

        private static void OnAddCopyLinkItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is FrameworkElement element) {
                if (e.NewValue is bool value && value) {
                    element.ContextMenuOpening += OnAddingCopyLinkItemMenuOpening;
                } else {
                    element.ContextMenuOpening -= OnAddingCopyLinkItemMenuOpening;
                }
            }
        }

        private static void OnAddingCopyLinkItemMenuOpening(object sender, ContextMenuEventArgs args) {
            if (((FrameworkElement)sender).ContextMenu is ContextMenu menu) {
                foreach (var item in menu.Items.OfType<FrameworkElement>().Where(x => x.Tag as string == @"copylink").ToList()) {
                    menu.Items.Remove(item);
                }

                if ((Mouse.DirectlyOver as Run)?.Parent is Hyperlink hyperlink && (hyperlink.Tag?.ToString()).IsWebUrl()) {
                    menu.Items.Insert(0, new MenuItem {
                        Header = "Copy link",
                        Command = new DelegateCommand(() => ClipboardHelper.SetText(hyperlink.Tag?.ToString())),
                        Tag = @"copylink"
                    });
                    menu.Items.Insert(1, new Separator {
                        Tag = @"copylink"
                    });
                }
            }
        }

        public static bool GetPropagateToChildren(DependencyObject obj) {
            return obj.GetValue(PropagateToChildrenProperty) as bool? == true;
        }

        public static void SetPropagateToChildren(DependencyObject obj, bool value) {
            obj.SetValue(PropagateToChildrenProperty, value);
        }

        public static readonly DependencyProperty PropagateToChildrenProperty = DependencyProperty.RegisterAttached("PropagateToChildren", typeof(bool),
                typeof(ContextMenuAdvancement), new UIPropertyMetadata(OnPropagateToChildrenChanged));

        private static void OnPropagateToChildrenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (!(d is FrameworkElement element) || !(e.NewValue is bool)) return;
            var newValue = (bool)e.NewValue;
            if (newValue) {
                element.PreviewMouseDown += OnMouseDown;
            } else {
                element.PreviewMouseDown -= OnMouseDown;
            }
        }

        internal static void Add(FrameworkElement parent, ContextMenu menu) {
            CheckTime();
            if (!ParentContextMenu.Any(x => ReferenceEquals(x.Item1, menu))) {
                ParentContextMenu.Add(Tuple.Create(menu, parent?.DataContext));
            }

            _lastClicked = DateTime.Now;
        }

        public static void PropagateToChildren(FrameworkElement parent) {
            var menu = (ContextMenu)parent.GetValue(FrameworkElement.ContextMenuProperty);
            if (menu != null) {
                Add(parent, menu);
            }
        }

        private static void OnMouseDown(object sender, MouseButtonEventArgs e) {
            PropagateToChildren((FrameworkElement)sender);
        }
    }
}