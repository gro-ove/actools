using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Helpers.Tyres;
using AcManager.Tools.Managers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public partial class ServerPresetObject {
        public class TyresItem {
            public TyresItem(string shortName, string displayName, [NotNull] IEnumerable<CarObject> cars) {
                if (cars == null) {
                    throw new ArgumentNullException(nameof(cars));
                }

                Cars = cars.ToList();
                CarsList = Cars.NonNull().Select(x => x.DisplayName).JoinToReadableString();
                ShortName = shortName;
                DisplayName = displayName;
                // DisplayName = $@"{displayName}: {CarsList}";
            }

            public string ShortName { get; }
            public string DisplayName { get; }
            public string CarsList { get; }
            public IReadOnlyList<CarObject> Cars { get; }
        }

        public BetterObservableCollection<TyresItem> Tyres { get; } = new BetterObservableCollection<TyresItem>();

        private List<TyresItem> _legalTyres = new List<TyresItem>();

        [NotNull]
        public IEnumerable<TyresItem> LegalTyres {
            get => _legalTyres;
            set {
                var list = value.ToList();
                if (list.SequenceEqual(_legalTyres)) return;
                _legalTyres = list;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private void UpdateTyresList() {
            var disabled = Tyres.ApartFrom(LegalTyres).ToList();
            Tyres.ReplaceEverythingBy_Direct(CarIds.Select(x => CarsManager.Instance.GetById(x)).NonNull()
                                                   .SelectMany(x => x.GetTyresSets()).Select(x => x.Front)
                                                   .GroupBy(x => x.ShortName).Select(x => new TyresItem(x.Key, x.First().Name, x.Select(y => y.Source)))
                                                   .OrderBy(x => x.ShortName));
            LegalTyres = Tyres.ApartFrom(disabled);
        }
    }
}