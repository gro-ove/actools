using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.CustomShowroom;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
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

            private DelegateCommand _createJsonCommand;

            public DelegateCommand CreateJsonCommand => _createJsonCommand ?? (_createJsonCommand = new DelegateCommand(() => {
                SelectedObject.SaveAsync();
            }));

            private DelegateCommand _deleteJsonCommand;

            public DelegateCommand DeleteJsonCommand => _deleteJsonCommand ?? (_deleteJsonCommand = new DelegateCommand(() => {
                try {
                    if (File.Exists(SelectedObject.JsonFilename)) {
                        FileUtils.Recycle(SelectedObject.JsonFilename);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify(AppStrings.Skin_CannotRemoveUiSkin, AppStrings.Skin_CannotRemoveUiSkin_Commentary, e);
                }
            }));

            private DelegateCommand _changeLiveryCommand;

            public DelegateCommand ChangeLiveryCommand => _changeLiveryCommand ??
                    (_changeLiveryCommand = new DelegateCommand(() => new LiveryIconEditorDialog(SelectedObject).ShowDialog()));

            private AsyncCommand _generateLiveryCommand;

            public AsyncCommand GenerateLiveryCommand => _generateLiveryCommand ??
                    (_generateLiveryCommand = new AsyncCommand(() => LiveryIconEditor.GenerateAsync(SelectedObject)));

            private AsyncCommand _generateRandomLiveryCommand;

            public AsyncCommand GenerateRandomLiveryCommand => _generateRandomLiveryCommand ??
                    (_generateRandomLiveryCommand = new AsyncCommand(() => LiveryIconEditor.GenerateRandomAsync(SelectedObject)));


            #region Auto-Update Previews
            private AsyncCommand _updatePreviewCommand;

            public AsyncCommand UpdatePreviewCommand => _updatePreviewCommand ??
                    (_updatePreviewCommand = new AsyncCommand(() => new ToUpdatePreview(Car, SelectedObject).Run(), () => SelectedObject.Enabled));

            private AsyncCommand _updatePreviewsManuallyCommand;

            public AsyncCommand UpdatePreviewManuallyCommand => _updatePreviewsManuallyCommand ??
                    (_updatePreviewsManuallyCommand = new AsyncCommand(() => new ToUpdatePreview(Car, SelectedObject).Run(UpdatePreviewMode.StartManual),
                            () => SelectedObject.Enabled));

            private AsyncCommand _updatePreviewsOptionsCommand;

            public AsyncCommand UpdatePreviewOptionsCommand => _updatePreviewsOptionsCommand ??
                    (_updatePreviewsOptionsCommand = new AsyncCommand(() => new ToUpdatePreview(Car, SelectedObject).Run(UpdatePreviewMode.Options),
                            () => SelectedObject.Enabled));
            #endregion

            #region Presets
            public HierarchicalItemsView ShowroomPresets {
                get => _showroomPresets;
                set => Apply(value, ref _showroomPresets);
            }

            public HierarchicalItemsView CustomShowroomPresets {
                get => _customShowroomPresets;
                set => Apply(value, ref _customShowroomPresets);
            }

            public HierarchicalItemsView UpdatePreviewsPresets {
                get => _updatePreviewsPresets;
                set => Apply(value, ref _updatePreviewsPresets);
            }

            public HierarchicalItemsView QuickDrivePresets {
                get => _quickDrivePresets;
                set => Apply(value, ref _quickDrivePresets);
            }

            private HierarchicalItemsView _showroomPresets, _customShowroomPresets, _updatePreviewsPresets, _quickDrivePresets;
            private readonly PresetsMenuHelper _helper = new PresetsMenuHelper();

            public void InitializeShowroomPresets() {
                if (ShowroomPresets == null) {
                    ShowroomPresets = _helper.Create(new PresetsCategory(CarOpenInShowroomDialog.PresetableKeyValue), p => {
                        CarOpenInShowroomDialog.RunPreset(p.VirtualFilename, Car, SelectedObject.Id);
                    });
                }
            }

            public void InitializeCustomShowroomPresets() {
                if (CustomShowroomPresets == null) {
                    CustomShowroomPresets = _helper.Create(new PresetsCategory(DarkRendererSettings.DefaultPresetableKeyValue), p => {
                        CustomShowroomWrapper.StartAsync(Car, SelectedObject, p.VirtualFilename);
                    });
                }
            }

            public void InitializeQuickDrivePresets() {
                if (QuickDrivePresets == null) {
                    QuickDrivePresets = _helper.Create(new PresetsCategory(QuickDrive.PresetableKeyValue), p => {
                        QuickDrive.RunAsync(Car, SelectedObject.Id, presetFilename: p.VirtualFilename).Forget();
                    });
                }
            }

            public void InitializeUpdatePreviewsPresets() {
                if (UpdatePreviewsPresets == null) {
                    UpdatePreviewsPresets = _helper.Create(new PresetsCategory(
                            SettingsHolder.CustomShowroom.CustomShowroomPreviews
                                    ? CmPreviewsSettings.DefaultPresetableKeyValue : CarUpdatePreviewsDialog.PresetableKeyValue),
                            p => new ToUpdatePreview(Car, SelectedObject).Run(p.VirtualFilename));
                }
            }
            #endregion

            #region Open In Showroom
            private DelegateCommand<object> _openInShowroomCommand;

            public DelegateCommand<object> OpenInShowroomCommand => _openInShowroomCommand ?? (_openInShowroomCommand = new DelegateCommand<object>(o => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                    OpenInCustomShowroomCommand.ExecuteAsync().Ignore();
                    return;
                }

                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                        !CarOpenInShowroomDialog.Run(Car, SelectedObject.Id)) {
                    OpenInShowroomOptionsCommand.Execute();
                }
            }, o => SelectedObject.Enabled));

            private DelegateCommand _openInShowroomOptionsCommand;

            public DelegateCommand OpenInShowroomOptionsCommand => _openInShowroomOptionsCommand ?? (_openInShowroomOptionsCommand = new DelegateCommand(() => {
                new CarOpenInShowroomDialog(Car, SelectedObject.Id).ShowDialog();
            }, () => SelectedObject.Enabled));

            private AsyncCommand _openInCustomShowroomCommand;

            public AsyncCommand OpenInCustomShowroomCommand => _openInCustomShowroomCommand ??
                    (_openInCustomShowroomCommand = new AsyncCommand(() => CustomShowroomWrapper.StartAsync(Car, SelectedObject)));

            private AsyncCommand _driveCommand;

            public AsyncCommand DriveCommand => _driveCommand ?? (_driveCommand = new AsyncCommand(async () => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                        !await QuickDrive.RunAsync(Car, SelectedObject.Id)) {
                    DriveOptionsCommand.Execute();
                }
            }, () => SelectedObject.Enabled));

            private DelegateCommand _driveOptionsCommand;

            public DelegateCommand DriveOptionsCommand => _driveOptionsCommand ?? (_driveOptionsCommand = new DelegateCommand(() => {
                QuickDrive.Show(Car, SelectedObject.Id);
            }, () => SelectedObject.Enabled));
            #endregion
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
                new InputBinding(_model.UpdatePreviewOptionsCommand, new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(_model.UpdatePreviewManuallyCommand, new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Alt)),

                new InputBinding(_model.GenerateLiveryCommand, new KeyGesture(Key.J, ModifierKeys.Alt)),
                new InputBinding(_model.GenerateLiveryCommand, new KeyGesture(Key.J, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(_model.GenerateRandomLiveryCommand, new KeyGesture(Key.J, ModifierKeys.Control | ModifierKeys.Alt)),

                new InputBinding(_model.DriveCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(_model.DriveOptionsCommand, new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Shift)),

                new InputBinding(_model.OpenInShowroomCommand, new KeyGesture(Key.H, ModifierKeys.Control)),
                new InputBinding(_model.OpenInShowroomOptionsCommand, new KeyGesture(Key.H, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(_model.OpenInCustomShowroomCommand, new KeyGesture(Key.H, ModifierKeys.Alt)),
                new InputBinding(_model.OpenInCustomShowroomCommand, new KeyGesture(Key.H, ModifierKeys.Alt | ModifierKeys.Control)),

                new InputBinding(_model.DeleteJsonCommand, new KeyGesture(Key.Delete, ModifierKeys.Alt)),
                new InputBinding(_model.CreateJsonCommand, new KeyGesture(Key.S, ModifierKeys.Alt)),
            });
            InitializeComponent();
        }

        private ViewModel _model;

        private void OnIconClick(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                _model.ChangeLiveryCommand.Execute();
            }
        }

        #region Presets (Dynamic Loading)
        private void OnShowroomButtonMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeShowroomPresets();
        }

        private void OnCustomShowroomButtonMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeCustomShowroomPresets();
        }

        private void OnDriveButtonMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeQuickDrivePresets();
        }

        private void OnUpdatePreviewsButtonMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeUpdatePreviewsPresets();
        }
        #endregion
    }
}
