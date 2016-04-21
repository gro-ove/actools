using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;

namespace AcManager.Controls.Helpers {
    public class PresetsMenuHelper : IDisposable {
        private readonly List<PresetsHandlerToRemove> _presetsHandlersToRemove = new List<PresetsHandlerToRemove>();

        private class PresetsHandlerToRemove {
            public string Key;
            public EventHandler Handler;
        }

        public static IEnumerable<MenuItem> GroupPresets(string presetsKey, Action<string> action) {
            return UserPresetsControl.GroupPresets(presetsKey, (sender, args) => {
                action(((UserPresetsControl.TagHelper)((MenuItem)sender).Tag).Entry.Filename);
            });
        } 
        
        public ObservableCollection<MenuItem> Create(string presetsKey, Action<string> action) {
            var result = new BetterObservableCollection<MenuItem>(GroupPresets(presetsKey, action));

            var handler = new EventHandler((sender, e) => result.ReplaceEverythingBy(GroupPresets(presetsKey, action)));
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