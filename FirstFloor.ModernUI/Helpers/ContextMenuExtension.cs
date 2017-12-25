using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FirstFloor.ModernUI.Commands;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public static class ContextMenuExtension {
        public static ContextMenu AddItem([NotNull] this ContextMenu menu, string header, ICommand command, string shortcut = null, object icon = null,
                Geometry iconData = null, bool isEnabled = true, string toolTip = null, Style style = null) {
            if (menu == null) throw new ArgumentNullException(nameof(menu));

            if (icon == null && iconData != null) {
                var path = new Path {
                    Data = iconData,
                    Width = 12,
                    Height = 12,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Stretch = Stretch.Uniform
                };
                path.SetBinding(Shape.FillProperty, new Binding {
                    Path = new PropertyPath("(Control.Foreground)"),
                    RelativeSource = new RelativeSource(RelativeSourceMode.Self)
                });
                icon = path;
            }

            var item = new MenuItem {
                Header = header,
                Icon = icon,
                IsEnabled = isEnabled,
                Command = command,
                ToolTip = toolTip,
                InputGestureText = shortcut
            };

            if (style != null) {
                item.Style = style;
            }

            menu.Items.Add(item);
            return menu;
        }

        public static ContextMenu AddItem([NotNull] this ContextMenu menu, string header, Action action, string shortcut = null, object icon = null,
                Geometry iconData = null, bool isEnabled = true) {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            return menu.AddItem(header, new DelegateCommand(action), shortcut, icon, iconData, isEnabled);
        }

        public static ContextMenu AddItem([NotNull] this ContextMenu menu, ICommand command) {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            menu.Items.Add(new MenuItem { Command = command });
            return menu;
        }

        public static ContextMenu AddItem([NotNull] this ContextMenu menu, MenuItem item) {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            menu.Items.Add(item);
            return menu;
        }

        public static ContextMenu AddSeparator([NotNull] this ContextMenu menu) {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            menu.Items.Add(new Separator());
            return menu;
        }

        public static ContextMenu AddTextBoxItems([NotNull] this ContextMenu menu) {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            if (!menu.Items.IsEmpty) {
                menu.AddSeparator();
            }

            return menu
                .AddItem(ApplicationCommands.Undo)
                .AddSeparator()
                .AddItem(ApplicationCommands.Cut)
                .AddItem(ApplicationCommands.Copy)
                .AddItem(ApplicationCommands.Paste)
                .AddItem(ApplicationCommands.Delete)
                .AddSeparator()
                .AddItem(ApplicationCommands.SelectAll);
        }
    }
}
