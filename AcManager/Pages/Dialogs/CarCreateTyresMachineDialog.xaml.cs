// ReSharper disable RedundantUsingDirective

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls;
using AcManager.Controls.ViewModels;
using AcManager.Tools;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcManager.Tools.Tyres;
using AcTools.DataFile;
using AcTools.NeuralTyres;
using AcTools.NeuralTyres.Data;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Attached;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Pages.Dialogs {
    public partial class CarCreateTyresMachineDialog {
        public static int OptionSupportedVersion = 10;

        private ViewModel Model => (ViewModel)DataContext;

        public CarCreateTyresMachineDialog(List<CarWithTyres> list) {
            DataContext = new ViewModel(list);
            InitializeComponent();
            this.OnActualUnload(Model);

            foreach (var s in Model.Cars.Where(x => x.IsChecked)) {
                CarsListBox.SelectedItems.Add(s);
            }

            CarsListBox.SelectionChanged += OnSelectionChanged;
        }

        public class CarTyres : NotifyPropertyChanged {
            public TyresEntry Entry { get; }

            public CarTyres(TyresEntry entry) {
                Entry = entry;
            }

            private bool _isChecked;

            public bool IsChecked {
                get => _isChecked;
                set {
                    if (Equals(value, _isChecked)) return;
                    _isChecked = value;
                    OnPropertyChanged();
                }
            }
        }

        public class CarWithTyres : NotifyPropertyChanged {
            public CarWithTyres(CarObject car, List<CarTyres> tyres) {
                Car = car;
                Tyres = tyres;
                Tyres.ForEach(x => x.PropertyChanged += OnTyrePropertyChanged);
                UpdateChecked();
            }

            private void OnTyrePropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(CarTyres.IsChecked)) {
                    UpdateChecked();
                }
            }

            public CarObject Car { get; }
            public List<CarTyres> Tyres { get; }

            private bool _isChecked;

            public bool IsChecked {
                get => _isChecked;
                set {
                    if (Equals(value, _isChecked)) return;
                    _isChecked = value;
                    OnPropertyChanged();
                }
            }

            private int _checkedCount;

            public int CheckedCount {
                get => _checkedCount;
                set {
                    if (Equals(value, _checkedCount)) return;
                    _checkedCount = value;
                    OnPropertyChanged();
                }
            }

            private string _displayChecked = ToolsStrings.Common_None;

            public string DisplayChecked {
                get => _displayChecked;
                set {
                    if (string.IsNullOrEmpty(value)) value = ToolsStrings.Common_None;
                    if (Equals(value, _displayChecked)) return;
                    _displayChecked = value;
                    OnPropertyChanged();
                }
            }

            private void UpdateChecked() {
                CheckedCount = Tyres.Count(x => x.IsChecked);
                DisplayChecked = Tyres.Where(x => x.IsChecked).Select(x => x.Entry.DisplayName).JoinToReadableString();
            }
        }

        private class ViewModel : NotifyPropertyChanged, IDisposable {
            public List<CarWithTyres> Cars { get; }
            public BetterListCollectionView CarsView { get; }

            public ViewModel(List<CarWithTyres> cars) {
                Cars = cars;
                UpdateListFilter();
                UpdateTyresFilter();
                UpdateSummary();

                CarsView = new BetterListCollectionView(Cars) {
                    Filter = o => {
                        var c = (CarWithTyres)o;
                        return (_listFilterInstance?.Test(c.Car) ?? true) && c.Tyres.Any(x => x.IsChecked);
                    }
                };

                Cars.ForEach(x => {
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

            private readonly StoredValue<string> _carsFilter = Stored.Get("/CreateTyres.CarsFilter", "kunos+");

            [CanBeNull]
            public string CarsFilter {
                get => _carsFilter.Value;
                set {
                    if (Equals(value, _carsFilter.Value)) return;
                    _carsFilter.Value = value;
                    OnPropertyChanged();
                    UpdateListFilter();
                }
            }

            private IFilter<CarObject> _listFilterInstance;

            private void UpdateListFilter() {
                _listFilterInstance = Filter.Create(CarObjectTester.Instance, CarsFilter ?? "*");
                CarsView?.Refresh();
            }

            private readonly StoredValue<string> _tyresFilter = Stored.Get("/CreateTyres.TyresFilter", "semislicks");

            [CanBeNull]
            public string TyresFilter {
                get => _tyresFilter.Value;
                set {
                    if (Equals(value, _tyresFilter.Value)) return;
                    _tyresFilter.Value = value;
                    OnPropertyChanged();
                    UpdateTyresFilter();
                }
            }

            private IFilter<TyresEntry> _tyresFilterInstance;

            private void UpdateTyresFilter() {
                _tyresFilterInstance = Filter.Create(TyresEntryTester.Instance, TyresFilter ?? "*");

                for (var i = Cars.Count - 1; i >= 0; i--) {
                    var car = Cars[i];
                    for (var j = car.Tyres.Count - 1; j >= 0; j--) {
                        var tyre = car.Tyres[j];
                        tyre.IsChecked = _tyresFilterInstance.Test(tyre.Entry);
                    }
                }

                UpdateSummary();
                CarsView?.Refresh();
            }

            private readonly Busy _updateSummaryBusy = new Busy();

            private void UpdateSummary() {
                _updateSummaryBusy.Yield(() => {
                    var tyres = Cars.SelectMany(x => x.IsChecked ? x.Tyres.Where(y => y.IsChecked) : new CarTyres[0]).Select(x => x.Entry)
                                    .Distinct(TyresEntry.TyresEntryComparer).ToList();

                    Radius.Reset();
                    RimRadius.Reset();
                    Width.Reset();

                    for (var i = tyres.Count - 1; i >= 0; i--) {
                        var tyre = tyres[i];
                        Radius.Update(tyre.Radius);
                        RimRadius.Update(tyre.RimRadius);
                        Width.Update(tyre.Width);
                    }

                    TotalTyresCount = Cars.Sum(x => x.IsChecked ? x.CheckedCount : 0);
                    UniqueTyresCount = tyres.Count;
                    Radius.Apply();
                    RimRadius.Apply();
                    Width.Apply();
                });
            }

            private int _totalTyresCount;

            public int TotalTyresCount {
                get => _totalTyresCount;
                set {
                    if (Equals(value, _totalTyresCount)) return;
                    _totalTyresCount = value;
                    OnPropertyChanged();
                }
            }

            private int _uniqueTyresCount;

            public int UniqueTyresCount {
                get => _uniqueTyresCount;
                set {
                    if (Equals(value, _uniqueTyresCount)) return;
                    _uniqueTyresCount = value;
                    OnPropertyChanged();
                    _createTyresMachineCommand?.RaiseCanExecuteChanged();
                }
            }

            public ValueLimits Radius { get; } = new ValueLimits(100, " cm");
            public ValueLimits RimRadius { get; } = new ValueLimits(100, " cm");
            public ValueLimits Width { get; } = new ValueLimits(100, " cm");

            private AsyncProgressEntry _createTyresMachineProgress = AsyncProgressEntry.Finished;

            public AsyncProgressEntry CreateTyresMachineProgress {
                get => _createTyresMachineProgress;
                set {
                    if (Equals(value, _createTyresMachineProgress)) return;
                    _createTyresMachineProgress = value;
                    OnPropertyChanged();
                }
            }

            private TyresMachine _generatedMachine;

            public TyresMachine GeneratedMachine {
                get => _generatedMachine;
                set {
                    if (Equals(value, _generatedMachine)) return;
                    _generatedMachine = value;
                    OnPropertyChanged();
                }
            }

            private AsyncCommand<CancellationToken?> _createTyresMachineCommand;

            public AsyncCommand<CancellationToken?> CreateTyresMachineCommand => _createTyresMachineCommand
                    ?? (_createTyresMachineCommand = new AsyncCommand<CancellationToken?>(CreateTyresMachine, c => UniqueTyresCount > 1));

            private async Task CreateTyresMachine(CancellationToken? c) {
                try {
                    var options = new NeuralTyresOptions {
                        TrainingRuns = 150000
                    };

                    var tyres = Cars.SelectMany(x => x.IsChecked ? x.Tyres.Where(y => y.IsChecked) : new CarTyres[0]).Select(x => x.Entry).Distinct(
                                    TyresEntry.TyresEntryComparer).Select(x => x.ToNeuralTyresEntry()).ToList();
                    var machine = await TyresMachine.CreateAsync(tyres, options, new Progress<Tuple<string, double?>>(
                            OnProgress));
                    CreateTyresMachineProgress = AsyncProgressEntry.Finished;
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t create tyres machine", e);
                }

                void OnProgress(Tuple<string, double?> p) {
                    ActionExtension.InvokeInMainThreadAsync(() => {
                        CreateTyresMachineProgress = new AsyncProgressEntry(p.Item1, p.Item2);
                    });
                }
            }

            public void Dispose() { }

            public async Task<TyresMachineInfo> CreateTyresMachineAsync() {
                return null;
            }
        }

        public class ValueLimits : NotifyPropertyChanged {
            public ValueLimits(double multiplier, string postfix) {
                _multiplier = multiplier;
                _postfix = postfix;
            }

            private readonly double _multiplier;
            private readonly string _postfix;

            public void Reset() {
                Minimum = double.MaxValue;
                Maximum = double.MinValue;
            }

            public void Update(double value) {
                if (Minimum > value) Minimum = value;
                if (Maximum < value) Maximum = value;
            }

            public void Apply() {
                var from = (Minimum * _multiplier).ToString(@"F1");
                var to = (Maximum * _multiplier).ToString(@"F1");
                DisplayValue = from == to ? from + _postfix : $@"{from}…{to}{_postfix}";
                OnPropertyChanged(nameof(Minimum));
                OnPropertyChanged(nameof(Maximum));
                OnPropertyChanged(nameof(DisplayValue));
            }

            public double Minimum { get; private set; } = double.MaxValue;
            public double Maximum { get; private set; } = double.MinValue;
            public string DisplayValue { get; private set; }
        }

        [ItemCanBeNull]
        public static async Task<TyresMachineInfo> RunAsync() {
            try {
                List<CarWithTyres> list;
                using (var waiting = WaitingDialog.Create(ControlsStrings.Common_Loading)) {
                    var cancellation = waiting.CancellationToken;

                    waiting.Report("Loading cars…");
                    await CarsManager.Instance.EnsureLoadedAsync();
                    cancellation.ThrowIfCancellationRequested();

                    list = await Task.Run(() => {
                        var l = new List<CarWithTyres>();
                        var c = CarsManager.Instance.Loaded.ToList();

                        for (var i = 0; i < c.Count; i++) {
                            var car = c[i];
                            waiting.Report(car.DisplayName, i, c.Count);
                            try {
                                var tyres = TyresSet.GetSets(car).SelectMany(x => new[] { new CarTyres(x.Front), new CarTyres(x.Rear) }).ToList();
                                if (tyres.Count > 0 && tyres[0].Entry.Version == OptionSupportedVersion) {
                                    l.Add(new CarWithTyres(car, tyres));
                                }
                            } catch (Exception e) {
                                Logging.Warning(e);
                            }
                            cancellation.ThrowIfCancellationRequested();
                        }

                        return l;
                    });

                    cancellation.ThrowIfCancellationRequested();
                }

                var dialog = new CarCreateTyresMachineDialog(list) {
                    Owner = null,
                    DoNotAttachToWaitingDialogs = true
                };

                dialog.ShowDialog();

                return dialog.IsResultOk ? await dialog.Model.CreateTyresMachineAsync() : null;
            } catch (Exception e) when (e.IsCanceled()) { } catch (Exception e) {
                NonfatalError.Notify("Can’t create new tyres machine", e);
            }

            return null;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            foreach (var c in e.AddedItems.OfType<CarWithTyres>()) {
                c.IsChecked = true;
            }
            foreach (var c in e.RemovedItems.OfType<CarWithTyres>()) {
                c.IsChecked = false;
            }
        }
    }
}