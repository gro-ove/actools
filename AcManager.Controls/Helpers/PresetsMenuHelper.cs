using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.Managers.Presets;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Controls.Helpers {
    public class PresetsMenuHelper : IDisposable {
        private readonly List<PresetsHandlerToRemove> _presetsHandlersToRemove = new List<PresetsHandlerToRemove>();

        private class PresetsHandlerToRemove {
            public string Key;
            public EventHandler Handler;
        }

        public static IEnumerable<object> GroupPresets(string presetsKey, [CanBeNull] Action<ISavedPresetEntry> action) {
            var group = new HierarchicalGroup("", UserPresetsControl.GroupPresets(presetsKey));
            var result = new HierarchicalItemsView((o, g) => {
                action?.Invoke((ISavedPresetEntry)o);
            }, group, false);
            return result;
        }

        public HierarchicalItemsView Create(string presetsKey, Action<ISavedPresetEntry> action, string displayName = "") {
            return new HierarchicalItemsView((o, g) => {
                action((ISavedPresetEntry)o);
            }, CreateGroup(presetsKey, displayName), false);
        }

        public HierarchicalGroup CreateGroup(string presetsKey, string displayName = "", string prependWithDefault = null) {
            Func<IEnumerable<object>> groupPresets = () => {
                var result = UserPresetsControl.GroupPresets(presetsKey);

                if (prependWithDefault != null) {
                    var menuItem = new HierarchicalItem {
                        Header = new TextBlock { Text = prependWithDefault, FontStyle = FontStyles.Italic }
                    };

                    HierarchicalItemsView.SetValue(menuItem, null);
                    result = result.Prepend(menuItem);
                }

                return result;
            };

            var group = new HierarchicalGroup(displayName, groupPresets());

            var handler = new EventHandler((sender, e) => {
                group.ReplaceEverythingBy(groupPresets());
            });

            PresetsManager.Instance.Watcher(presetsKey).Update += handler;
            _presetsHandlersToRemove.Add(new PresetsHandlerToRemove { Key = presetsKey, Handler = handler });

            return group;
        }

        public void Dispose() {
            foreach (var presetsHandlerToRemove in _presetsHandlersToRemove) {
                PresetsManager.Instance.Watcher(presetsHandlerToRemove.Key).Update -= presetsHandlerToRemove.Handler;
            }

            _presetsHandlersToRemove.Clear();
        }
    }
}