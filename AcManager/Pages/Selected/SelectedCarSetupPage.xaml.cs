using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    public partial class SelectedCarSetupPage : ILoadableContent, IParametrizedUriContent {
        public class ViewModel : SelectedAcObjectViewModel<CarSetupObject> {
            public CarObject Car { get; }

            public SettingEntry[] Tyres { get; }

            private SettingEntry _selectedTyres;

            public SettingEntry SelectedTyres {
                get { return _selectedTyres; }
                set {
                    if (!Tyres.Contains(value)) value = Tyres[0];
                    if (Equals(value, _selectedTyres)) return;
                    _selectedTyres = value;
                    OnPropertyChanged();
                    SelectedObject.Tyres = value?.IntValue ?? 0;
                }
            }

            public ViewModel(CarObject car, [NotNull] CarSetupObject acObject) : base(acObject) {
                Car = car;

                var main = Car.AcdData.GetIniFile("car.ini");
                SelectedObject.FuelMaximum = main["FUEL"].GetInt("MAX_FUEL", 0);

                var tyres = Car.AcdData.GetIniFile("tyres.ini");
                Tyres = tyres.GetSections("FRONT", -1).Select((x, i) => new SettingEntry(i, x.GetPossiblyEmpty("NAME"))).ToArray();
                SelectedTyres = Tyres.ElementAtOrDefault(SelectedObject.Tyres);

                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(acObject, nameof(PropertyChanged), Handler);
            }

            private RelayCommand _changeTrackCommand;

            public RelayCommand ChangeTrackCommand => _changeTrackCommand ?? (_changeTrackCommand = new RelayCommand(o => {
                var dialog = new SelectTrackDialog(SelectedObject.Track);
                dialog.ShowDialog();
                if (!dialog.IsResultOk || dialog.Model.SelectedTrackConfiguration == null) return;
                SelectedObject.Track = dialog.Model.SelectedTrack;
            }));

            private RelayCommand _clearTrackCommand;

            public RelayCommand ClearTrackCommand => _clearTrackCommand ?? (_clearTrackCommand = new RelayCommand(o => {
                SelectedObject.TrackId = null;
                ClearTrackCommand.OnCanExecuteChanged();
            }, o => SelectedObject.TrackId != null));

            private void Handler(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(SelectedObject.Tyres):
                        SelectedTyres = Tyres.ElementAtOrDefault(SelectedObject.Tyres);
                        break;
                }
            }

            private AsyncCommand _shareCommand;

            public AsyncCommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(o => {
                var data = SharingHelper.SetMetadata(SharedEntryType.CarSetup, FileUtils.ReadAllText(SelectedObject.Location),
                        new SharedMetadata {
                            [@"car"] = Car.Id,
                            [@"track"] = SelectedObject.TrackId
                        });
                var target = SelectedObject.Track == null ? Car.DisplayName : $"{Car.DisplayName} ({SelectedObject.Track.DisplayName})";
                return SharingUiHelper.ShareAsync(SharedEntryType.CarSetup, SelectedObject.Name, target, data);
            }));

            private AsyncCommand _testCommand;

            public AsyncCommand TestCommand => _testCommand ?? (_testCommand = new AsyncCommand(o => {
                var setupId = SelectedObject.Id.ApartFromLast(SelectedObject.Extension).Replace('\\', '/');
                return QuickDrive.RunAsync(Car, track: SelectedObject.Track, carSetupId: setupId);
            }));
        }

        private string _carId, _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _carId = uri.GetQueryParam("CarId");
            if (_carId == null) throw new ArgumentException(ToolsStrings.Common_CarIdIsMissing);

            _id = uri.GetQueryParam("Id");
            if (_id == null) throw new ArgumentException(ToolsStrings.Common_IdIsMissing);
        }

        private CarObject _carObject;
        private CarSetupObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            do {
                _carObject = await CarsManager.Instance.GetByIdAsync(_carId);
                if (_carObject == null) {
                    _object = null;
                    return;
                }

                await Task.Run(() => {
                    _carObject.AcdData.GetIniFile("car.ini");
                    _carObject.AcdData.GetIniFile("tyres.ini");
                }, cancellationToken);

                _object = await _carObject.SetupsManager.GetByIdAsync(_id);
            } while (_carObject.Outdated);
        }

        void ILoadableContent.Load() {
            do {
                _carObject = CarsManager.Instance.GetById(_carId);
                if (_carObject == null) {
                    _object = null;
                    return;
                }

                _object = _carObject?.SetupsManager.GetById(_id);
            } while (_carObject.Outdated);
        }

        void ILoadableContent.Initialize() {
            if (_carObject == null) throw new ArgumentException(AppStrings.Common_CannotFindCarById);
            if (_object == null) throw new ArgumentException(AppStrings.Common_CannotFindObjectById);

            InitializeAcObjectPage(_model = new ViewModel(_carObject, _object));
            InputBindings.AddRange(new[] {
                new InputBinding(_model.TestCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(_model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
            });
            InitializeComponent();
        }

        private ViewModel _model;
    }
}
