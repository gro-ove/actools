using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers {
    public class DriverModelsManager : AcManagerFileSpecific<DriverModelObject> {
        private static DriverModelsManager _instance;

        public static DriverModelsManager Instance => _instance ?? (_instance = new DriverModelsManager());

        [CanBeNull]
        public DriverModelObject GetByAcId(string v) {
            return GetById(v + DriverModelObject.FileExtension);
        }

        public string DefaultFilename => Directories?.GetLocation("driver.kn5", true);

        protected override bool FilterId(string id) {
            return base.FilterId(id) && !id.EndsWith(@"_B.kn5");
        }

        public override string SearchPattern => @"*.kn5";

        protected override string CheckIfIdValid(string id) {
            if (!id.EndsWith(DriverModelObject.FileExtension, StringComparison.OrdinalIgnoreCase)) {
                return $"ID should end with “{DriverModelObject.FileExtension}”.";
            }

            return base.CheckIfIdValid(id);
        }

        public override DriverModelObject GetDefault() {
            var v = WrappersList.FirstOrDefault(x => x.Value.Id.Contains(@"driver.kn5"));
            return v == null ? base.GetDefault() : EnsureWrapperLoaded(v);
        }

        public override IAcDirectories Directories => AcRootDirectory.Instance.DriverModelsDirectories;

        protected override DriverModelObject CreateAcObject(string id, bool enabled) {
            return new DriverModelObject(this, id, enabled);
        }

        public DateTime? LastUsingsRescan {
            get => ValuesStorage.Get<DateTime?>("DriverModelsManager.LastUsingsRescan");
            set {
                if (Equals(value, LastUsingsRescan)) return;

                if (value.HasValue) {
                    ValuesStorage.Set("DriverModelsManager.LastUsingsRescan", value.Value);
                } else {
                    ValuesStorage.Remove("DriverModelsManager.LastUsingsRescan");
                }

                OnPropertyChanged();
            }
        }

        public async Task<List<string>> UsingsRescan(IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default) {
            try {
                await EnsureLoadedAsync();
                if (cancellation.IsCancellationRequested) return null;

                await CarsManager.Instance.EnsureLoadedAsync();
                if (cancellation.IsCancellationRequested) return null;

                var i = 0;
                var cars = CarsManager.Instance.Loaded.ToList();

                var list = (await cars.Select(async car => {
                    if (cancellation.IsCancellationRequested) return null;

                    progress?.Report(car.DisplayName, i++, cars.Count);
                    return new {
                        CarId = car.Id,
                        DriverModelId = (await Task.Run(() => car.AcdData?.GetIniFile(@"driver3d.ini"), cancellation))
                                ["MODEL"].GetNonEmpty("NAME")
                    };
                }).WhenAll(12, cancellation)).Where(x => x?.DriverModelId != null).ToListIfItIsNot();

                if (cancellation.IsCancellationRequested) return null;
                foreach (var fontObject in Loaded) {
                    fontObject.UsingsCarsIds = list.Where(x => x.DriverModelId == fontObject.AcId).Select(x => x.CarId).ToArray();
                }

                return list.Select(x => x.DriverModelId).Distinct().Where(id => GetWrapperById(id + DriverModelObject.FileExtension) == null).ToList();
            } catch (Exception e) when (e.IsCancelled()) {
                return null;
            } catch (Exception e) {
                NonfatalError.Notify(ToolsStrings.Fonts_RescanUsings, e);
                return null;
            } finally {
                LastUsingsRescan = DateTime.Now;
            }
        }

        private CommandBase _usedRescanCommand;

        public ICommand UsingsRescanCommand => _usedRescanCommand ?? (_usedRescanCommand = new AsyncCommand(() => UsingsRescan()));
    }
}