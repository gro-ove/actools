using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FirstFloor.ModernUI.Windows.Attached;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class InheritingContextMenu : ContextMenu {
        public static bool OptionInsertInFront = false;

        private static readonly object TemporaryMark = new object();
        private static WeakReference<InheritingContextMenu> _previouslyOpened;

        private List<ContextMenu> _menus;
        private List<List<FrameworkElement>> _items;

        public InheritingContextMenu() {
            DefaultStyleKey = typeof(InheritingContextMenu);
        }

        private void RemoveTemporary() {
            if (_items == null) return;

            foreach (var item in Items.OfType<FrameworkElement>().Where(x => ReferenceEquals(x.Tag, TemporaryMark)).ToList()) {
                Items.Remove(item);
                if (item is MenuItem) {
                    _menus[_items.FindIndex(x => x.Contains(item))].Items.Add(item);
                    item.Tag = null;
                }
            }

            _menus = null;
            _items.Clear();
            _items = null;
        }

        protected override void OnOpened(RoutedEventArgs e) {
            InheritingContextMenu previous = null;
            if (_previouslyOpened?.TryGetTarget(out previous) == true) {
                previous.RemoveTemporary();
            }

            RemoveTemporary();
            ContextMenuAdvancement.CheckTime();

            _menus = ContextMenuAdvancement.ParentContextMenu;
            if (_menus.Count == 0) return;

            _menus = _menus.Where(x => !ReferenceEquals(x, this)).ToList();
            ContextMenuAdvancement.ParentContextMenu.Clear();

            _items = new List<List<FrameworkElement>>(_menus.Count);
            foreach (var menu in _menus) {
                _items.Add(menu.Items.OfType<FrameworkElement>().ToList());
                menu.Items.Clear();
            }

            if (OptionInsertInFront) {
                var i = 0;
                foreach (var group in _items) {
                    var any = false;
                    foreach (var item in group) {
                        Items.Insert(i++, item);
                        item.Tag = TemporaryMark;
                        any = true;
                    }

                    if (any) {
                        Items.Insert(i++, new Separator { Tag = TemporaryMark });
                    }
                }
            } else {
                foreach (var group in _items) {
                    var any = false;
                    foreach (var item in group) {
                        if (!any) {
                            Items.Add(new Separator { Tag = TemporaryMark });
                        }

                        Items.Add(item);
                        item.Tag = TemporaryMark;
                        any = true;
                    }
                }
            }

            _previouslyOpened = new WeakReference<InheritingContextMenu>(this);
            base.OnOpened(e);
        }

        protected override void OnClosed(RoutedEventArgs e) {
            RemoveTemporary();
            base.OnClosed(e);
        }
    }
}