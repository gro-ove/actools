using System.IO.Compression;
using System.Windows;
using AcManager.Tools.Helpers;
using AcTools.NeuralTyres;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Tyres {
    public class TyresMachineExtras : NotifyPropertyChanged, ITyresMachineExtras {
        private string _tyresName;

        [CanBeNull]
        public string TyresName {
            get => _tyresName;
            set => Apply(value, ref _tyresName);
        }

        private string _tyresShortName;

        [CanBeNull]
        public string TyresShortName {
            get => _tyresShortName;
            set => Apply(value, ref _tyresShortName);
        }

        private FrameworkElement _icon;

        [CanBeNull]
        public FrameworkElement Icon {
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
                Icon = icon.Invoke().Get();
                TyresName = tyresName;
                TyresShortName = tyresShortName;
            });
        }
    }
}