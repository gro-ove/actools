using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcTools.NeuralTyres;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Tyres {
    public sealed class TyresMachineInfo : Displayable {
        private TyresMachine _machine;

        public string Description { get; }

        public TyresMachineInfo(string displayName, TyresMachine machine) {
            _machine = machine;
            DisplayName = displayName;
            Description = _machine.Sources.Select(x => x.CarId + "/" + x.Name).JoinToReadableString();
        }

        private string _filename;
        private DateTime _lastWriteTime;

        private static ChangeableObservableCollection<TyresMachineInfo> _list;
        private static readonly TaskCache ListTaskCache = new TaskCache();

        public static Task<ChangeableObservableCollection<TyresMachineInfo>> LoadMachinesAsync() {
            return _list != null ? Task.FromResult(_list) : ListTaskCache.Get(LoadMachinesAsyncInner);
        }

        private static async Task<ChangeableObservableCollection<TyresMachineInfo>> LoadMachinesAsyncInner() {
            FilesStorage.Instance.Watcher(ContentCategory.NeuralTyresMachines).Update += OnUpdate;
            _list = new ChangeableObservableCollection<TyresMachineInfo>(await Task.Run(() => GetList()));
            return _list;

            IEnumerable<TyresMachineInfo> GetList() {
                return FilesStorage.Instance.GetContentFiles(ContentCategory.NeuralTyresMachines)
                                   .TryToSelect(CreateTyresMachineInfo, e => NonfatalError.NotifyBackground("Failed to load tyres machine", e))
                                   .ToList();
            }
        }

        private static TyresMachineInfo CreateTyresMachineInfo(FilesStorage.ContentEntry x) {
            return new TyresMachineInfo(Path.GetFileNameWithoutExtension(x.Filename), TyresMachine.LoadFrom(x.Filename)) {
                _filename = x.Filename,
                _lastWriteTime = x.LastWriteTime
            };
        }

        private static void OnUpdate(object sender, EventArgs eventArgs) {
            _list?.ReplaceEverythingBy_Direct(
                    FilesStorage.Instance.GetContentFiles(ContentCategory.NeuralTyresMachines).TryToSelect(x => {
                        return _list.FirstOrDefault(y => y._filename == x.Filename && y._lastWriteTime == x.LastWriteTime) ?? CreateTyresMachineInfo(x);
                    }));
        }
    }
}