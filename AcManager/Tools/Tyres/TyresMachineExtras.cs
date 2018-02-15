using System.IO.Compression;
using AcManager.Tools.Helpers;
using AcTools.NeuralTyres;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Tyres {
    public class TyresMachineExtras : NotifyPropertyChanged, ITyresMachineExtras {
        private string _tyresName;

        public string TyresName {
            get => _tyresName;
            set => Apply(value, ref _tyresName);
        }

        private string _tyresShortName;

        public string TyresShortName {
            get => _tyresShortName;
            set => Apply(value, ref _tyresShortName);
        }

        private object _icon;

        public object Icon {
            get => _icon;
            set => Apply(value, ref _icon);
        }

        public void OnSave(ZipArchive archive, JObject manifest, TyresMachine machine) {
            manifest[@"tyresName"] = TyresName;
            manifest[@"tyresShortName"] = TyresShortName;
        }

        public virtual void OnLoad(ZipArchive archive, JObject manifest, TyresMachine machine) {
            var icon = ContentUtils.GetIconInTwoSteps(manifest.GetStringValueOnly("icon"), archive.ReadBytes);
            var tyresName = manifest.GetStringValueOnly("tyresName");
            var tyresShortName = manifest.GetStringValueOnly("tyresShortName");
            ActionExtension.InvokeInMainThreadAsync(() => {
                Icon = icon?.Invoke();
                TyresName = tyresName;
                TyresShortName = tyresShortName;
            });
        }
    }
}