using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using AcManager.Controls;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;

namespace AcManager.Pages.Selected {
    public class PresetsMenuHelper {
        private static readonly List<PresetsHandlerToRemove> PresetsHandlersToRemove = new List<PresetsHandlerToRemove>();

        private class PresetsHandlerToRemove {
            public string Key;
            public EventHandler Handler;
        }
        
        public static ObservableCollection<MenuItem> CreatePresetsMenu(string presetsKey, Action<string> action) {
            var result = new BetterObservableCollection<MenuItem>();

            Action rebuildPresets = () => result.ReplaceEverythingBy(UserPresetsControl.GroupPresets(presetsKey, (sender, args) => {
                action(((UserPresetsControl.TagHelper)((MenuItem)sender).Tag).Entry.Filename);
            }));
            rebuildPresets();

            var updateHandler = new EventHandler((sender, e) => rebuildPresets());
            PresetsManager.Instance.Watcher(presetsKey).Update += updateHandler;
            PresetsHandlersToRemove.Add(new PresetsHandlerToRemove { Key = presetsKey, Handler = updateHandler });

            return result;
        }

        public static void UnloadPresetsWatchers() {
            foreach (var presetsHandlerToRemove in PresetsHandlersToRemove) {
                PresetsManager.Instance.Watcher(presetsHandlerToRemove.Key).Update -= presetsHandlerToRemove.Handler;
            }

            PresetsHandlersToRemove.Clear();
        }
    }
}