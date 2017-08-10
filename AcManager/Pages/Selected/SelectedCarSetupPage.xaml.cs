using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Tools;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    public partial class SelectedCarSetupPage : ILoadableContent, IParametrizedUriContent, IImmediateContent {
        public class ViewModel : SelectedAcObjectViewModel<CarSetupObject> {
            public CarObject Car { get; }

            public CarSetupValues SetupValues { get; }

            public ViewModel(CarObject car, [NotNull] CarSetupObject acObject, [CanBeNull] ViewModel previous) : base(acObject) {
                Car = car;
                SetupValues = new CarSetupValues(car, acObject, previous?.SetupValues);
            }

            public override void Unload() {
                SetupValues.Dispose();
                base.Unload();
            }

            private CommandBase _changeTrackCommand;

            public ICommand ChangeTrackCommand => _changeTrackCommand ?? (_changeTrackCommand = new DelegateCommand(() => {
                SelectedObject.Track = SelectTrackDialog.Show(SelectedObject.Track)?.MainTrackObject;
            }));

            private CommandBase _clearTrackCommand;

            public ICommand ClearTrackCommand => _clearTrackCommand ?? (_clearTrackCommand = new DelegateCommand(() => {
                SelectedObject.TrackId = null;
                _clearTrackCommand?.RaiseCanExecuteChanged();
            }, () => SelectedObject.TrackId != null));

            private CommandBase _shareCommand;

            public ICommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(() => {
                var data = SharingHelper.SetMetadata(SharedEntryType.CarSetup, FileUtils.ReadAllText(SelectedObject.Location),
                        new SharedMetadata {
                            [@"car"] = Car.Id,
                            [@"track"] = SelectedObject.TrackId
                        });
                var target = SelectedObject.Track == null ? Car.Name : $"{Car.Name} ({SelectedObject.Track.Name})";
                return SharingUiHelper.ShareAsync(SharedEntryType.CarSetup, SelectedObject.Name, target, data);
            }));

            private CommandBase _testCommand;

            public ICommand TestCommand => _testCommand ?? (_testCommand = new AsyncCommand(() => {
                var setupId = SelectedObject.Id.ApartFromLast(SelectedObject.Extension).Replace('\\', '/');
                return QuickDrive.RunAsync(Car, track: SelectedObject.Track, carSetupId: setupId);
            }));

            protected override string PrepareIdForInput(string id) {
                if (string.IsNullOrWhiteSpace(id)) return null;
                return Path.GetFileName(id.ApartFromLast(SelectedObject.Extension, StringComparison.OrdinalIgnoreCase));
            }

            protected override string FixIdFromInput(string id) {
                if (string.IsNullOrWhiteSpace(id)) return null;
                return Path.Combine(Path.GetDirectoryName(SelectedObject.Id) ?? CarSetupObject.GenericDirectory,
                        id + SelectedObject.Extension);
            }
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
                    _carObject.AcdData?.GetIniFile("car.ini");
                    _carObject.AcdData?.GetIniFile("setup.ini");
                    _carObject.AcdData?.GetIniFile("tyres.ini");
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

            SetModel();
            InitializeComponent();
        }

        private void SetModel() {
            _model?.Unload();
            InitializeAcObjectPage(_model = new ViewModel(_carObject, _object, _model));
            InputBindings.AddRange(new[] {
                new InputBinding(_model.TestCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(_model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
            });
        }

        public bool ImmediateChange(Uri uri) {
            var carId = uri.GetQueryParam("CarId");
            if (carId == null || carId != _carId) return false;

            var id = uri.GetQueryParam("Id");
            if (id == null) return false;

            var obj = _carObject?.SetupsManager.GetById(id);
            if (obj == null) return false;

            _object = obj;
            SetModel();
            return true;
        }

        private ViewModel _model;
    }
}
