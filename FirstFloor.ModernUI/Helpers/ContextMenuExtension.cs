using System;
using System.Windows.Controls;
using System.Windows.Input;

namespace FirstFloor.ModernUI.Helpers {
    public static class ContextMenuExtension {
        public static ContextMenu AddItem(this ContextMenu menu, string header, Action action, string shortcut = null) {
            var item = new MenuItem { Header = header };
            if (shortcut != null) item.InputGestureText = shortcut;
            item.Click += (sender, args) => action();
            menu.Items.Add(item);
            return menu;
        }

        public static ContextMenu AddItem(this ContextMenu menu, ICommand command) {
            menu.Items.Add(new MenuItem { Command = command });
            return menu;
        }

        public static ContextMenu AddSeparator(this ContextMenu menu) {
            menu.Items.Add(new Separator());
            return menu;
        }

        public static ContextMenu AddTextBoxItems(this ContextMenu menu) {
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
