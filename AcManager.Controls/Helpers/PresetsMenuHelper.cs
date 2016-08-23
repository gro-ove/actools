using System;
using System.Collections.Generic;
using AcManager.Tools.Managers.Presets;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Controls.Helpers {
    public class PresetsMenuHelper : IDisposable {
        private readonly List<PresetsHandlerToRemove> _presetsHandlersToRemove = new List<PresetsHandlerToRemove>();

        private class PresetsHandlerToRemove {
            public string Key;
            public EventHandler Handler;
        }
        public static IEnumerable<object> GroupPresets(string presetsKey, Action<string> action) {
            var group = new HierarchicalGroup("", UserPresetsControl.GroupPresets(presetsKey));
            var result = new HierarchicalItemsView(o => {
                action(((ISavedPresetEntry)o).Filename);
            }, group, false);
            return result;
        }

        public HierarchicalItemsView Create(string presetsKey, Action<string> action) {
            var group = new HierarchicalGroup("", UserPresetsControl.GroupPresets(presetsKey));
            var result = new HierarchicalItemsView(o => {
                action(((ISavedPresetEntry)o).Filename);
            }, group, false);

            var handler = new EventHandler((sender, e) => group.ReplaceEverythingBy(UserPresetsControl.GroupPresets(presetsKey)));
            PresetsManager.Instance.Watcher(presetsKey).Update += handler;
            _presetsHandlersToRemove.Add(new PresetsHandlerToRemove { Key = presetsKey, Handler = handler });

            return result;
        }

        public void Dispose() {
            foreach (var presetsHandlerToRemove in _presetsHandlersToRemove) {
                PresetsManager.Instance.Watcher(presetsHandlerToRemove.Key).Update -= presetsHandlerToRemove.Handler;
            }

            _presetsHandlersToRemove.Clear();
        }
    }
}