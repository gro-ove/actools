using System;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.SemiGui;
using AcManager.Tools.SharedMemory;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Pages.Drive {
    public static class RaceUTemporarySkinsHelper {
        public static void Initialize() {
            AcSharedMemory.Instance.Start += (sender, args) => CleanUp();
            GameWrapper.Ended += (sender, args) => CleanUp();
        }

        private const string _key = ".raceUTemporarySkins";
        private static DateTime _pauseCleanUp;

        public static void CleanUp() {
            ActionExtension.InvokeInMainThreadAsync(() => {
                if (DateTime.Now < _pauseCleanUp) return;

                var list = ValuesStorage.Storage.GetStringList(_key).ToIReadOnlyListIfItIsNot();
                if (list.Count <= 0) return;

                ValuesStorage.Storage.SetStringList(_key, list.Skip(1));
                Task.Run(() => {
                    FileUtils.TryToDeleteDirectory(list[0]);
                    ActionExtension.InvokeInMainThreadAsync(() => CleanUp());
                });
            });
        }

        public static void MarkForFutherRemoval(string skinDirectory) {
            ActionExtension.InvokeInMainThreadAsync(() => {
                _pauseCleanUp = DateTime.Now + TimeSpan.FromMinutes(2d);
                ValuesStorage.Storage.SetStringList(_key, ValuesStorage.Storage.GetStringList(_key).Union(new[]{ skinDirectory }));
            });
        }
    }
}