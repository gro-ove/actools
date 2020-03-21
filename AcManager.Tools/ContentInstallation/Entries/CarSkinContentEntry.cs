using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public class CarSkinContentEntry : ContentEntryBase<CarSkinObject> {
        public override double Priority => 30d;

        [NotNull]
        private readonly CarObject _car;

        public CarSkinContentEntry([NotNull] string path, [NotNull] string id, [NotNull] string carId, string name = null, byte[] iconData = null)
                : base(path, id, name, null, iconData) {
            _car = CarsManager.Instance.GetById(carId) ?? throw new Exception($"Car “{carId}” for the skin not found");
            NewFormat = string.Format(ToolsStrings.ContentInstallation_CarSkinNew, "{0}", _car.DisplayName);
            ExistingFormat = string.Format(ToolsStrings.ContentInstallation_CarSkinExisting, "{0}", _car.DisplayName);
        }

        public override string GenericModTypeName => "Car skin";
        public override string NewFormat { get; }
        public override string ExistingFormat { get; }

        public override FileAcManager<CarSkinObject> GetManager() {
            return _car.SkinsManager;
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            bool UiFilter(string x) {
                return !FileUtils.ArePathsEqual(x, @"ui_skin.json");
            }

            bool PreviewFilter(string x) {
                return !FileUtils.ArePathsEqual(x, @"preview.jpg");
            }

            return base.GetUpdateOptions().Union(new[] {
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformation, false){ Filter = UiFilter },
                new UpdateOption("Update over existing files, keep preview", false) { Filter = PreviewFilter },
                new UpdateOption("Update over existing files, keep UI information & preview", false) { Filter = x => UiFilter(x) && PreviewFilter(x) }
            });
        }
    }
}