using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Data;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Objects;
using AcManager.Tools.Tyres;
using AcTools.NeuralTyres.Data;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Pages.Dialogs {
    public partial class CarCreateTyresMachineDialog {
        private class ExampleCarsViewModel : NotifyPropertyChanged, IUserPresetable {
            public List<CarWithTyres> CarsList { get; }
            public BetterListCollectionView CarsView { get; }
            public BetterObservableCollection<CommonTyres> CommonTyresList { get; }

            private class Data {
                public Dictionary<string, Tuple<bool, string[]>> Checked;
                public string Filter = "kunos+", TyresName, TyresShortName;
            }

            private readonly ISaveHelper _saveable;

            public ExampleCarsViewModel(List<CarWithTyres> cars) {
                CommonTyresList = new BetterObservableCollection<CommonTyres>();

                CarsList = cars;
                UpdateListFilterLater();
                UpdateSummary();

                CarsView = new BetterListCollectionView(CarsList) {
                    Filter = o => _listFilterInstance?.Test(((CarWithTyres)o).Car) ?? true
                };

                TestKeys = new BetterObservableCollection<SettingEntry>();
                TestKeysView = new BetterListCollectionView(TestKeys) {
                    GroupDescriptions = {
                        new PropertyGroupDescription(nameof(SettingEntry.Tag))
                    },
                    CustomSort = KeyComparer.Instance
                };

                (_saveable = new SaveHelper<Data>("CreateTyres.ExampleCars", () => new Data {
                    Checked = CarsList.ToDictionary(x => x.Car.Id,
                            x => Tuple.Create(x.IsChecked, x.Tyres.Where(y => y.IsChecked).Select(y => y.Entry.DisplayName).ToArray())),
                    Filter = CarsFilter,
                    TyresName = TyresName,
                    TyresShortName = TyresShortName,
                }, o => {
                    for (var i = CarsList.Count - 1; i >= 0; i--) {
                        var c = CarsList[i];
                        var e = o.Checked?.GetValueOrDefault(c.Car.Id);
                        c.IsChecked = o.Checked == null ? c.Car.Author == AcCommonObject.AuthorKunos : e?.Item1 == true;
                        for (var j = c.Tyres.Count - 1; j >= 0; j--) {
                            var tyre = c.Tyres[j];
                            tyre.IsChecked = e?.Item2.ArrayContains(tyre.Entry.DisplayName) == true;
                        }
                    }
                    CarsFilter = o.Filter;
                    TyresName = o.TyresName;
                    TyresShortName = o.TyresShortName;
                })).Initialize();
                UpdateListFilter();

                CarsList.ForEach(x => {
                    x.PropertyChanged += (sender, args) => {
                        if (args.PropertyName == nameof(CarWithTyres.IsChecked)) {
                            UpdateSummary();
                        }
                    };

                    x.Tyres.ForEach(y => {
                        y.PropertyChanged += (sender, args) => {
                            if (args.PropertyName == nameof(CarTyres.IsChecked)) {
                                UpdateSummary();
                            }
                        };
                    });
                });
            }

            #region Utils
            bool IUserPresetable.CanBeSaved => true;
            string IUserPresetable.PresetableKey => ExampleCarsParamsKey;
            PresetsCategory IUserPresetable.PresetableCategory => ExampleCarsParamsCategory;

            public event EventHandler Changed;

            public void ImportFromPresetData(string data) {
                _saveable.FromSerializedString(data);
            }

            public string ExportToPresetData() {
                return _saveable.ToSerializedString();
            }

            private void SaveLater() {
                _saveable.SaveLater();
                Changed?.Invoke(this, EventArgs.Empty);
            }
            #endregion

            #region Methods
            private IEnumerable<CommonTyres> GetCommonTyres() {
                var result = new List<CommonTyres>();
                var items = new Dictionary<string, CommonTyres>();

                foreach (CarWithTyres v in CarsView) {
                    for (var i = v.Tyres.Count - 1; i >= 0; i--) {
                        var name = v.Tyres[i].Entry.Name;
                        if (!items.TryGetValue(name, out var gen)) {
                            items[name] = gen = new CommonTyres(name, ChangeTyresStateCallback);
                            result.Add(gen);
                        }

                        gen.Count++;
                        if (!gen.Cars.Contains(v.Car)) {
                            gen.Cars.Add(v.Car);
                        }
                    }
                }

                result.Sort(CommonTyresComparer.Instance);
                return result;
            }

            private class CommonTyresComparer : IComparer<CommonTyres> {
                public static readonly CommonTyresComparer Instance = new CommonTyresComparer();

                public int Compare(CommonTyres x, CommonTyres y) {
                    return y?.Count - x?.Count ?? 0;
                }
            }

            private void ChangeTyresStateCallback(string s, bool b) {
                foreach (var tyre in CarsList.SelectMany(x => x.Tyres).Where(x => x.Entry.Name == s)) {
                    tyre.IsChecked = b;
                }
            }

            private void UpdateIncludedCounters() {
                foreach (var item in CommonTyresList) {
                    item.IncludedCount = CarsList.Sum(x => x.IsChecked ? x.Tyres.Count(y => y.IsChecked && y.Entry.Name == item.DisplayName) : 0);
                }
            }
            #endregion

            #region Tyres selection
            private string _carsFilter;

            [CanBeNull]
            public string CarsFilter {
                get => _carsFilter;
                set => Apply(value, ref _carsFilter, UpdateListFilterLater);
            }

            private IFilter<CarObject> _listFilterInstance;
            private readonly Busy _updateListFilterBusy = new Busy();

            private void UpdateListFilter() {
                _listFilterInstance = Filter.Create(CarObjectTester.Instance, CarsFilter.Or(@"*"));
                if (CarsView == null) return;
                CarsView.Refresh();
                CommonTyresList.ReplaceEverythingBy_Direct(GetCommonTyres());
                UpdateIncludedCounters();
            }

            private void UpdateListFilterLater() {
                _updateListFilterBusy.DoDelay(UpdateListFilter, 100);
            }

            public TyresEntry[] SelectedTyres;
            private readonly Busy _updateSummaryBusy = new Busy();

            private void UpdateSummary() {
                _updateSummaryBusy.Yield(() => {
                    var tyres = CarsList.SelectMany(x => x.IsChecked ? x.Tyres.Where(y => y.IsChecked) : new CarTyres[0]).Select(x => x.Entry)
                                        .Distinct(TyresEntry.TyresEntryComparer).ToArray();
                    SelectedTyres = tyres;

                    Radius.Reset();
                    RimRadius.Reset();
                    Width.Reset();

                    for (var i = tyres.Length - 1; i >= 0; i--) {
                        var tyre = tyres[i];
                        Radius.Update(tyre.Radius);
                        RimRadius.Update(tyre.RimRadius);
                        Width.Update(tyre.Width);
                    }

                    TotalTyresCount = CarsList.Sum(x => x.IsChecked ? x.CheckedCount : 0);
                    UniqueTyresCount = tyres.Length;
                    Radius.Apply(ValuePadding, AcNormalizationLimits.Default.GetLimits(NeuralTyresOptions.InputRadius));
                    RimRadius.Apply(ValuePadding, AcNormalizationLimits.Default.GetLimits(NeuralTyresOptions.InputRimRadius));
                    Width.Apply(ValuePadding, AcNormalizationLimits.Default.GetLimits(NeuralTyresOptions.InputWidth));

                    if (SelectedTyres.Length > 0) {
                        var testKey = SelectedTestKey;
                        var keys = SelectedTyres[0].MainSection.Keys.Concat(SelectedTyres[0].ThermalSection.Keys.Select(x => TyresExtension.ThermalPrefix + x));
                        TestKeys.ReplaceEverythingBy_Direct(keys.Where(x => !NeuralTyresOptions.Default.IgnoredKeys.ArrayContains(x)).Select(GetOutputKeyEntry));
                        SelectedTestKey = TestKeys.GetByIdOrDefault(testKey.Value) ?? testKey;
                        TyresNames = tyres.GroupBy(x => x.Name).OrderByDescending(x => x.Count()).Select(x => x.Key).ToList();
                        TyresShortNames = tyres.GroupBy(x => x.ShortName).OrderByDescending(x => x.Count()).Select(x => x.Key).ToList();
                    }

                    UpdateIncludedCounters();
                    SaveLater();
                });
            }

            private List<string> _tyresNames;

            public IReadOnlyList<string> TyresNames {
                get => _tyresNames;
                set => Apply(value.ToListIfItIsNot(), ref _tyresNames);
            }

            private List<string> _tyresShortNames;

            public IReadOnlyList<string> TyresShortNames {
                get => _tyresShortNames;
                set => Apply(value.ToListIfItIsNot(), ref _tyresShortNames);
            }

            private string _tyresName;

            public string TyresName {
                get => _tyresName;
                set => Apply(value, ref _tyresName, () => {
                    SaveLater();
                    RaiseInvalidatePlotModel();
                });
            }

            private string _tyresShortName;

            public string TyresShortName {
                get => _tyresShortName;
                set => Apply(value, ref _tyresShortName, () => {
                    SaveLater();
                    RaiseInvalidatePlotModel();
                });
            }

            private int _totalTyresCount;

            public int TotalTyresCount {
                get => _totalTyresCount;
                set => Apply(value, ref _totalTyresCount);
            }

            private int _uniqueTyresCount;

            public int UniqueTyresCount {
                get => _uniqueTyresCount;
                set => Apply(value, ref _uniqueTyresCount);
            }

            public DisplayDoubleRange Radius { get; } = new DisplayDoubleRange(100, " cm");
            public DisplayDoubleRange RimRadius { get; } = new DisplayDoubleRange(100, " cm");
            public DisplayDoubleRange Width { get; } = new DisplayDoubleRange(100, " cm");

            private readonly StoredValue<double> _valuePadding = Stored.Get("/CreateTyres.ValuePadding", 0.3);

            public double ValuePadding {
                get => _valuePadding.Value;
                set => Apply(value, _valuePadding, UpdateSummary);
            }
            #endregion

            #region Testing key (to quickly check a single param)
            private readonly StoredValue<bool> _testSingleKey = Stored.Get("/CreateTyres.TestSingleKey", false);

            public bool TestSingleKey {
                get => _testSingleKey.Value;
                set => Apply(value, _testSingleKey);
            }

            [NotNull]
            public BetterObservableCollection<SettingEntry> TestKeys { get; }

            public BetterListCollectionView TestKeysView { get; }

            private SettingEntry _selectedTestKey = DefaultOutputKey;

            public SettingEntry SelectedTestKey {
                get => _selectedTestKey;
                set => Apply(TestKeys.Contains(value) ? value
                        : TestKeys.GetByIdOrDefault(_selectedTestKey?.Value) ?? TestKeys.FirstOrDefault() ?? DefaultOutputKey,
                        ref _selectedTestKey, RaiseInvalidatePlotModel);
            }
            #endregion

            private void RaiseInvalidatePlotModel() {
                InvalidatePlotModel?.Invoke(this, EventArgs.Empty);
            }

            public event EventHandler InvalidatePlotModel;

            /*
             * TODO: SAVE SELECTED CARS
             */
        }
    }
}