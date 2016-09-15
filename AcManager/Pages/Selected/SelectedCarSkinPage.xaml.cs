using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Pages.Dialogs;
using AcManager.Tools;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Pages.Selected {
    public partial class SelectedCarSkinPage : ILoadableContent, IParametrizedUriContent {
        public class ViewModel : SelectedAcObjectViewModel<CarSkinObject> {
            public CarObject Car { get; }

            public ViewModel(CarObject car, [NotNull] CarSkinObject acObject) : base(acObject) {
                Car = car;
            }
            
            protected override void FilterExec(string type) {
                switch (type) {
                    case "team":
                        NewFilterTab(string.IsNullOrWhiteSpace(SelectedObject.Team) ? @"team-" : $"team:{Filter.Encode(SelectedObject.Team)}");
                        break;

                    case "driver":
                        NewFilterTab(string.IsNullOrWhiteSpace(SelectedObject.DriverName) ? @"driver-" : $"driver:{Filter.Encode(SelectedObject.DriverName)}");
                        break;

                    case "number":
                        NewFilterTab(string.IsNullOrWhiteSpace(SelectedObject.SkinNumber) ? @"number-" : $"number:{Filter.Encode(SelectedObject.SkinNumber)}");
                        break;

                    case "priority":
                        NewFilterTab(SelectedObject.Priority.HasValue ? $"priority:{SelectedObject.Priority.Value}" : @"priority-");
                        break;
                }
                
                base.FilterExec(type);
            }

            private ProperCommand _createJsonCommand;

            public ICommand CreateJsonCommand => _createJsonCommand ?? (_createJsonCommand = new ProperCommand(o => {
                SelectedObject.Save();
            }));

            private ProperCommand _deleteJsonCommand;

            public ICommand DeleteJsonCommand => _deleteJsonCommand ?? (_deleteJsonCommand = new ProperCommand(o => {
                try {
                    if (File.Exists(SelectedObject.JsonFilename)) {
                        FileUtils.Recycle(SelectedObject.JsonFilename);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify(AppStrings.Skin_CannotRemoveUiSkin, AppStrings.Skin_CannotRemoveUiSkin_Commentary, e);
                }
            }));

            private ProperCommand _updatePreviewCommand;

            public ICommand UpdatePreviewCommand => _updatePreviewCommand ?? (_updatePreviewCommand = new ProperCommand(o => {
                new CarUpdatePreviewsDialog(Car, new[] { SelectedObject.Id },
                        SelectedCarPage.ViewModel.GetAutoUpdatePreviewsDialogMode()).ShowDialog();
            }, o => SelectedObject.Enabled));

            private ProperCommand _changeLiveryCommand;

            public ICommand ChangeLiveryCommand => _changeLiveryCommand ?? (_changeLiveryCommand = new ProperCommand(o => {
                new LiveryIconEditor(SelectedObject).ShowDialog();
            }));

            private ProperAsyncCommand _generateLiveryCommand;

            public ICommand GenerateLiveryCommand
                => _generateLiveryCommand ?? (_generateLiveryCommand = new ProperAsyncCommand(o => LiveryIconEditor.GenerateAsync(SelectedObject)));

            private ProperAsyncCommand _generateRandomLiveryCommand;

            public ICommand GenerateRandomLiveryCommand
                => _generateRandomLiveryCommand ?? (_generateRandomLiveryCommand = new ProperAsyncCommand(o => LiveryIconEditor.GenerateRandomAsync(SelectedObject)));
        }

        private string _carId, _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _carId = uri.GetQueryParam("CarId");
            if (_carId == null) throw new ArgumentException(ToolsStrings.Common_CarIdIsMissing);

            _id = uri.GetQueryParam("Id");
            if (_id == null) throw new ArgumentException(ToolsStrings.Common_IdIsMissing);
        }

        private CarObject _carObject;
        private CarSkinObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            do {
                _carObject = await CarsManager.Instance.GetByIdAsync(_carId);
                if (_carObject == null) {
                    _object = null;
                    return;
                }

                _object = await _carObject.SkinsManager.GetByIdAsync(_id);
            } while (_carObject.Outdated);
        }

        void ILoadableContent.Load() {
            do {
                _carObject = CarsManager.Instance.GetById(_carId);
                if (_carObject == null) {
                    _object = null;
                    return;
                }

                _object = _carObject?.SkinsManager.GetById(_id);
            } while (_carObject.Outdated);
        }

        void ILoadableContent.Initialize() {
            if (_carObject == null) throw new ArgumentException(AppStrings.Common_CannotFindCarById);
            if (_object == null) throw new ArgumentException(AppStrings.Common_CannotFindObjectById);

            InitializeAcObjectPage(_model = new ViewModel(_carObject, _object));
            InputBindings.AddRange(new[] {
                new InputBinding(_model.UpdatePreviewCommand, new KeyGesture(Key.P, ModifierKeys.Control)),
                new InputBinding(_model.GenerateLiveryCommand, new KeyGesture(Key.L, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(_model.GenerateRandomLiveryCommand, new KeyGesture(Key.L, ModifierKeys.Control | ModifierKeys.Alt)),
                new InputBinding(_model.DeleteJsonCommand, new KeyGesture(Key.Delete, ModifierKeys.Alt)),
                new InputBinding(_model.CreateJsonCommand, new KeyGesture(Key.S, ModifierKeys.Alt)),
            });
            InitializeComponent();
        }

        private ViewModel _model;

        private void AcObjectBase_OnIconMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                _model.ChangeLiveryCommand.Execute(null);
            }
        }
    }
}
