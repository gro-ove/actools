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
            public PresetsCategory Key;
            public EventHandler Handler;
        }

        public static IEnumerable<object> GroupPresets(PresetsCategory category, [CanBeNull] Action<ISavedPresetEntry> action) {
            var group = new HierarchicalGroup("", UserPresetsControl.GroupPresets(category));
            var result = new HierarchicalItemsView((o, g) => {
                action?.Invoke((ISavedPresetEntry)o);
            }, group, false);
            return result;
        }

        public HierarchicalItemsView Create(PresetsCategory category, Action<ISavedPresetEntry> action, string displayName = "") {
            return new HierarchicalItemsView((o, g) => {
                action((ISavedPresetEntry)o);
            }, CreateGroup(category, displayName), false);
        }

        public HierarchicalGroup CreateGroup(PresetsCategory category, string displayName = "", string prependWithDefault = null) {
            IEnumerable<object> Presets() {
                var result = UserPresetsControl.GroupPresets(category);

                if (prependWithDefault != null) {
                    var menuItem = new HierarchicalItem {
                        Header = new TextBlock { Text = prependWithDefault, FontStyle = FontStyles.Italic }
                    };

                    HierarchicalItemsView.SetValue(menuItem, null);
                    result = result.Prepend(menuItem);
                }

                return result;
            }

            var group = new HierarchicalGroup(displayName, Presets());

            var handler = new EventHandler((sender, e) => {
                group.ReplaceEverythingBy(Presets());
            });

            PresetsManager.Instance.Watcher(category).Update += handler;
            _presetsHandlersToRemove.Add(new PresetsHandlerToRemove { Key = category, Handler = handler });

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