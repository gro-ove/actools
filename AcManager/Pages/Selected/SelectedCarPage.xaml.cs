using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JetBrains.Annotations;
using AcManager.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Controls.Presentation;
using AcManager.CustomShowroom;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Pages.Lists;
using AcManager.Tools;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Objects;
using AcTools.AcdFile;
using AcTools.Utils;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using StringBasedFilter;
using MenuItem = System.Windows.Controls.MenuItem;

namespace AcManager.Pages.Selected {
    public partial class SelectedCarPage : ILoadableContent, IParametrizedUriContent, IImmediateContent {
        public class ViewModel : SelectedAcObjectViewModel<CarObject> {
            public ViewModel([NotNull] CarObject acObject) : base(acObject) {
                AcContext.Instance.CurrentCar = acObject;
            }

            public override void Load() {
                base.Load();
                SelectedObject.PropertyChanged += SelectedObject_PropertyChanged;
            }

            public override void Unload() {
                base.Unload();
                SelectedObject.PropertyChanged -= SelectedObject_PropertyChanged;
                _helper.Dispose();
            }

            private void SelectedObject_PropertyChanged(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(AcCommonObject.Year):
                        InnerFilterCommand?.RaiseCanExecuteChanged();
                        break;
                    case nameof(CarObject.Brand):
                        if (SettingsHolder.Content.ChangeBrandIconAutomatically) {
                            var entry = FilesStorage.Instance.GetContentFile(ContentCategory.BrandBadges, SelectedObject.Brand + @".png");
                            if (entry.Exists) {
                                try {
                                    FileUtils.Recycle(SelectedObject.BrandBadge);
                                    File.Copy(entry.Filename, SelectedObject.BrandBadge);
                                } catch (Exception ex) {
                                    Logging.Warning(ex);
                                }
                            }
                        }
                        break;
                }
            }

            protected override void FilterExec(string type) {
                switch (type) {
                    case "class":
                        NewFilterTab(string.IsNullOrWhiteSpace(SelectedObject.CarClass) ? @"class-" : $"class:{Filter.Encode(SelectedObject.CarClass)}");
                        break;

                    case "brand":
                        NewFilterTab(string.IsNullOrWhiteSpace(SelectedObject.Brand) ? @"brand-" : $"brand:{Filter.Encode(SelectedObject.Brand)}");
                        break;

                    case "power":
                        FilterRange("power", SelectedObject.SpecsBhp);
                        break;

                    case "torque":
                        FilterRange("torque", SelectedObject.SpecsTorque);
                        break;

                    case "weight":
                        FilterRange("weight", SelectedObject.SpecsWeight);
                        break;

                    case "pwratio":
                        FilterRange("pwratio", SelectedObject.SpecsPwRatio, roundTo: 0.01);
                        break;

                    case "topspeed":
                        FilterRange("topspeed", SelectedObject.SpecsTopSpeed);
                        break;

                    case "acceleration":
                        FilterRange("acceleration", SelectedObject.SpecsAcceleration, roundTo: 0.1);
                        break;
                }

                base.FilterExec(type);
            }

            private static WeakReference<ModernDialog> _analyzerDialog;
            private DelegateCommand _carAnalyzerCommand;

            public DelegateCommand CarAnalyzerCommand => _carAnalyzerCommand ?? (_carAnalyzerCommand = new DelegateCommand(() => {
                if (_analyzerDialog != null && _analyzerDialog.TryGetTarget(out ModernDialog dialog)) {
                    dialog.Close();
                }

                dialog = new ModernDialog {
                    ShowTitle = false,
                    Title = "Analyzer",
                    SizeToContent = SizeToContent.Manual,
                    ResizeMode = ResizeMode.CanResizeWithGrip,
                    LocationAndSizeKey = @"lsMigrationHelper",
                    MinWidth = 800,
                    MinHeight = 480,
                    Width = 800,
                    Height = 640,
                    MaxWidth = 99999,
                    MaxHeight = 99999,
                    Content = new ModernFrame {
                        Source = UriExtension.Create("/Pages/ContentTools/CarAnalyzer.xaml?Id={0}&Models=True&Rating=True", SelectedObject.Id)
                    }
                };

                dialog.Show();
                _analyzerDialog = new WeakReference<ModernDialog>(dialog);
            }));

            #region Open In Showroom
            private CommandBase _openInShowroomCommand;

            public ICommand OpenInShowroomCommand => _openInShowroomCommand ?? (_openInShowroomCommand = new DelegateCommand<object>(o => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                    OpenInCustomShowroomCommand.Execute(o);
                    return;
                }

                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                        !CarOpenInShowroomDialog.Run(SelectedObject, SelectedObject.SelectedSkin?.Id)) {
                    OpenInShowroomOptionsCommand.Execute(null);
                }
            }, o => SelectedObject.Enabled && SelectedObject.SelectedSkin != null));

            private CommandBase _openInShowroomOptionsCommand;

            public ICommand OpenInShowroomOptionsCommand => _openInShowroomOptionsCommand ?? (_openInShowroomOptionsCommand = new DelegateCommand(() => {
                new CarOpenInShowroomDialog(SelectedObject, SelectedObject.SelectedSkin?.Id).ShowDialog();
            }, () => SelectedObject.Enabled && SelectedObject.SelectedSkin != null));

            private CommandBase _openInCustomShowroomCommand;

            public ICommand OpenInCustomShowroomCommand => _openInCustomShowroomCommand ??
                    (_openInCustomShowroomCommand = new AsyncCommand(() => CustomShowroomWrapper.StartAsync(SelectedObject, SelectedObject.SelectedSkin)));

            private CommandBase _driveCommand;

            public ICommand DriveCommand => _driveCommand ?? (_driveCommand = new DelegateCommand(() => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                        !QuickDrive.Run(SelectedObject, SelectedObject.SelectedSkin?.Id)) {
                    DriveOptionsCommand.Execute(null);
                }
            }, () => SelectedObject.Enabled));

            private CommandBase _driveOptionsCommand;

            public ICommand DriveOptionsCommand => _driveOptionsCommand ?? (_driveOptionsCommand = new DelegateCommand(() => {
                QuickDrive.Show(SelectedObject, SelectedObject.SelectedSkin?.Id);
            }, () => SelectedObject.Enabled));
            #endregion

            #region Auto-Update Previews
            private ICommand _updatePreviewsCommand;

            public ICommand UpdatePreviewsCommand => _updatePreviewsCommand ??
                    (_updatePreviewsCommand = new AsyncCommand(() => new ToUpdatePreview(SelectedObject).Run(), () => SelectedObject.Enabled));

            private ICommand _updatePreviewsManuallyCommand;

            public ICommand UpdatePreviewsManuallyCommand => _updatePreviewsManuallyCommand ??
                    (_updatePreviewsManuallyCommand = new AsyncCommand(() => new ToUpdatePreview(SelectedObject).Run(UpdatePreviewMode.StartManual),
                            () => SelectedObject.Enabled));

            private ICommand _updatePreviewsOptionsCommand;

            public ICommand UpdatePreviewsOptionsCommand => _updatePreviewsOptionsCommand ??
                    (_updatePreviewsOptionsCommand = new AsyncCommand(() => new ToUpdatePreview(SelectedObject).Run(UpdatePreviewMode.Options),
                            () => SelectedObject.Enabled));
            #endregion

            #region Presets
            public HierarchicalItemsView ShowroomPresets {
                get { return _showroomPresets; }
                set {
                    if (Equals(value, _showroomPresets)) return;
                    _showroomPresets = value;
                    OnPropertyChanged();
                }
            }

            public HierarchicalItemsView CustomShowroomPresets {
                get { return _customShowroomPresets; }
                set {
                    if (Equals(value, _customShowroomPresets)) return;
                    _customShowroomPresets = value;
                    OnPropertyChanged();
                }
            }

            public HierarchicalItemsView UpdatePreviewsPresets {
                get { return _updatePreviewsPresets; }
                set {
                    if (Equals(value, _updatePreviewsPresets)) return;
                    _updatePreviewsPresets = value;
                    OnPropertyChanged();
                }
            }

            public HierarchicalItemsView QuickDrivePresets {
                get { return _quickDrivePresets; }
                set {
                    if (Equals(value, _quickDrivePresets)) return;
                    _quickDrivePresets = value;
                    OnPropertyChanged();
                }
            }

            private HierarchicalItemsView _showroomPresets, _customShowroomPresets, _updatePreviewsPresets, _quickDrivePresets;
            private readonly PresetsMenuHelper _helper = new PresetsMenuHelper();

            public void InitializeShowroomPresets() {
                if (ShowroomPresets == null) {
                    ShowroomPresets = _helper.Create(new PresetsCategory(CarOpenInShowroomDialog.PresetableKeyValue), p => {
                        CarOpenInShowroomDialog.RunPreset(p.Filename, SelectedObject, SelectedObject.SelectedSkin?.Id);
                    });
                }
            }

            public void InitializeCustomShowroomPresets() {
                if (CustomShowroomPresets == null) {
                    CustomShowroomPresets = _helper.Create(new PresetsCategory(DarkRendererSettings.DefaultPresetableKeyValue), p => {
                        CustomShowroomWrapper.StartAsync(SelectedObject, SelectedObject.SelectedSkin, p.Filename);
                    });
                }
            }

            public void InitializeQuickDrivePresets() {
                if (QuickDrivePresets == null) {
                    QuickDrivePresets = _helper.Create(new PresetsCategory(QuickDrive.PresetableKeyValue), p => {
                        QuickDrive.RunPreset(p.Filename, SelectedObject, SelectedObject.SelectedSkin?.Id);
                    });
                }
            }

            public void InitializeUpdatePreviewsPresets() {
                if (UpdatePreviewsPresets == null) {
                    UpdatePreviewsPresets = _helper.Create(new PresetsCategory(
                            SettingsHolder.CustomShowroom.CustomShowroomPreviews
                                    ? CmPreviewsSettings.DefaultPresetableKeyValue : CarUpdatePreviewsDialog.PresetableKeyValue),
                            p => new ToUpdatePreview(SelectedObject).Run(p.Filename));
                }
            }
            #endregion

            private CommandBase _manageSkinsCommand;

            public ICommand ManageSkinsCommand => _manageSkinsCommand ?? (_manageSkinsCommand = new DelegateCommand(() => {
                CarSkinsListPage.Open(SelectedObject);
            }));

            private CommandBase _manageSetupsCommand;

            public ICommand ManageSetupsCommand => _manageSetupsCommand ?? (_manageSetupsCommand = new DelegateCommand(() => {
                CarSetupsListPage.Open(SelectedObject);
            }));

            private string DataDirectory => Path.Combine(SelectedObject.Location, "data");

            private CommandBase _readDataCommand;

            public ICommand ReadDataCommand => _readDataCommand ?? (_readDataCommand = new DelegateCommand(() => {
                var source = Path.Combine(SelectedObject.Location, "data.a" + "cd");
                try {
                    var destination = FileUtils.EnsureUnique(DataDirectory);
                    Acd.FromFile(source).ExportDirectory(destination);
                    WindowsHelper.ViewDirectory(destination);
                } catch (Exception e) {
                    NonfatalError.Notify(ToolsStrings.Common_CannotReadData, e);
                }
            }, () => SettingsHolder.Common.MsMode && SelectedObject.AcdData?.IsPacked == true));

            private CommandBase _packDataCommand;

            public ICommand PackDataCommand => _packDataCommand ?? (_packDataCommand = new DelegateCommand(() => {
                try {
                    var destination = Path.Combine(SelectedObject.Location, "data.a" + "cd");
                    var exists = File.Exists(destination);

                    if (SelectedObject.Author == AcCommonObject.AuthorKunos && ModernDialog.ShowMessage(
                            AppStrings.Car_PackKunosDataMessage, ToolsStrings.Common_Warning, MessageBoxButton.YesNo) != MessageBoxResult.Yes ||
                            SelectedObject.Author != AcCommonObject.AuthorKunos && exists && ModernDialog.ShowMessage(
                                    AppStrings.Car_PackExistingDataMessage, ToolsStrings.Common_Warning, MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                        return;
                    }

                    if (exists) {
                        FileUtils.Recycle(destination);
                    }

                    Acd.FromDirectory(DataDirectory).Save(destination);
                    WindowsHelper.ViewFile(destination);
                } catch (Exception e) {
                    NonfatalError.Notify(AppStrings.Car_CannotPackData, ToolsStrings.Common_MakeSureThereIsEnoughSpace, e);
                }
            }, () => SettingsHolder.Common.DeveloperMode && Directory.Exists(DataDirectory)));

            private CommandBase _replaceSoundCommand;

            public ICommand ReplaceSoundCommand => _replaceSoundCommand ??
                    (_replaceSoundCommand = new AsyncCommand(() => CarSoundReplacer.Replace(SelectedObject)));

            private AsyncCommand _replaceTyresCommand;

            public AsyncCommand ReplaceTyresCommand => _replaceTyresCommand ??
                    (_replaceTyresCommand = new AsyncCommand(() => CarReplaceTyresDialog.Run(SelectedObject)));
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception(ToolsStrings.Common_IdIsMissing);
            }
        }

        private CarObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await CarsManager.Instance.GetByIdAsync(_id);
            if (_object == null) return;
            await _object.SkinsManager.EnsureLoadedAsync();
        }

        void ILoadableContent.Load() {
            _object = CarsManager.Instance.GetById(_id);
            _object?.SkinsManager.EnsureLoaded();
        }

        bool IImmediateContent.ImmediateChange(Uri uri) {
            var id = uri.GetQueryParam("Id");
            if (id == null) return false;

            var obj = CarsManager.Instance.GetById(id);
            if (obj == null) return false;

            _object.SkinsManager.EnsureLoadedAsync().Forget();

            _id = id;
            _object = obj;
            SetModel();
            return true;
        }

        private ViewModel _model;

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException(AppStrings.Common_CannotFindObjectById);

            SetModel();
            InitializeComponent();

            if (!AppAppearanceManager.Instance.PopupToolBars) {
                FancyHints.AccidentallyRemoved.Trigger();
            }
        }

        private void SetModel() {
            _model?.Unload();
            InitializeAcObjectPage(_model = new ViewModel(_object));
            InputBindings.AddRange(new[] {
                new InputBinding(_model.UpdatePreviewsCommand, new KeyGesture(Key.P, ModifierKeys.Control)),
                new InputBinding(_model.UpdatePreviewsOptionsCommand, new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(_model.UpdatePreviewsManuallyCommand, new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Alt)),

                new InputBinding(_model.DriveCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(_model.DriveOptionsCommand, new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Shift)),

                new InputBinding(_model.OpenInShowroomCommand, new KeyGesture(Key.H, ModifierKeys.Control)),
                new InputBinding(_model.OpenInShowroomOptionsCommand, new KeyGesture(Key.H, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(_model.OpenInCustomShowroomCommand, new KeyGesture(Key.H, ModifierKeys.Alt)),
                new InputBinding(_model.OpenInCustomShowroomCommand, new KeyGesture(Key.H, ModifierKeys.Alt | ModifierKeys.Control)),

                new InputBinding(_model.ManageSkinsCommand, new KeyGesture(Key.K, ModifierKeys.Control)),
                new InputBinding(_model.ManageSetupsCommand, new KeyGesture(Key.U, ModifierKeys.Control)),

                new InputBinding(_model.PackDataCommand, new KeyGesture(Key.J, ModifierKeys.Control)),
                new InputBinding(_model.ReadDataCommand, new KeyGesture(Key.J, ModifierKeys.Alt)),
            });
        }

        #region Skins
        private void SelectedSkinPreview_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount == 2 && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                e.Handled = true;
                CarOpenInShowroomDialog.Run(_model.SelectedObject, _model.SelectedObject.SelectedSkin?.Id);
            } else if (e.ClickCount == 1 && ReferenceEquals(sender, SelectedSkinPreviewImage) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                e.Handled = true;

                var skins = _model.SelectedObject.EnabledOnlySkins.ToList();
                new ImageViewer(
                        from skin in skins select skin.PreviewImage,
                        skins.IndexOf(_model.SelectedObject.SelectedSkin),
                        1022).ShowDialog();
            }
        }

        private void SelectedSkinPreview_MouseUp(object sender, MouseButtonEventArgs e) {
            e.Handled = true;

            var context = ((FrameworkElement)sender).DataContext;
            var wrapper = context as AcItemWrapper;
            OpenSkinContextMenu((wrapper?.Value ?? context) as CarSkinObject);
        }

        private void OpenSkinContextMenu(CarSkinObject skin) {
            if (skin == null) return;

            var contextMenu = new ContextMenu {
                Items = {
                    new MenuItem {
                        Header = string.Format(AppStrings.Car_SkinFormat, skin.DisplayName?.Replace(@"_", @"__") ?? @"?"),
                        StaysOpenOnClick = true
                    }
                }
            };

            var item = new MenuItem { Header = ControlsStrings.Car_OpenInShowroom, InputGestureText = @"Ctrl+H" };
            item.Click += (sender, args) => CarOpenInShowroomDialog.Run(_model.SelectedObject, skin.Id);
            contextMenu.Items.Add(item);

            item = new MenuItem { Header = ControlsStrings.Car_OpenInCustomShowroom, InputGestureText = @"Alt+H" };
            item.Click += (sender, args) => CustomShowroomWrapper.StartAsync(_model.SelectedObject, skin);
            contextMenu.Items.Add(item);

            contextMenu.Items.Add(new MenuItem {
                Header = AppStrings.Toolbar_Folder,
                Command = skin.ViewInExplorerCommand
            });

            contextMenu.Items.Add(new Separator());

            item = new MenuItem { Header = AppStrings.Toolbar_UpdatePreview };
            item.Click += (sender, args) => new ToUpdatePreview(_model.SelectedObject, skin).Run();
            contextMenu.Items.Add(item);

            contextMenu.Items.Add(new Separator());

            item = new MenuItem { Header = AppStrings.Toolbar_ChangeLivery };
            item.Click += (sender, args) => new LiveryIconEditorDialog(skin).ShowDialog();
            contextMenu.Items.Add(item);

            item = new MenuItem { Header = AppStrings.Toolbar_GenerateLivery, ToolTip = AppStrings.Solution_GenerateLivery_Details };
            item.Click += (sender, args) => LiveryIconEditor.GenerateAsync(skin).Forget();
            contextMenu.Items.Add(item);

            item = new MenuItem { Header = AppStrings.Toolbar_GenerateRandomLivery, ToolTip = AppStrings.Solution_RandomLivery_Details };
            item.Click += (sender, args) => LiveryIconEditor.GenerateRandomAsync(skin).Forget();
            contextMenu.Items.Add(item);

            contextMenu.Items.Add(new Separator());

            contextMenu.Items.Add(new MenuItem {
                Header = AppStrings.Car_DeleteSkin,
                ToolTip = AppStrings.Car_DeleteSkin_Tooltip,
                Command = skin.DeleteCommand
            });

            contextMenu.IsOpen = true;
        }
        #endregion

        #region Presets (Dynamic Loading)
        private void OnShowroomButtonMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeShowroomPresets();
        }

        private void OnCustomShowroomButtonMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeCustomShowroomPresets();
        }

        private void OnQuickDriveButtonMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeQuickDrivePresets();
        }

        private void OnUpdatePreviewsButtonMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeUpdatePreviewsPresets();
        }
        #endregion

        #region Icons & Specs
        private void OnIconClick(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                new BrandBadgeEditor((CarObject)SelectedAcObject).ShowDialog();
            }
        }

        private void UpgradeIcon_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                e.Handled = true;
                new UpgradeIconEditor((CarObject)SelectedAcObject).ShowDialog();
            }
        }

        private void ParentBlock_OnMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                e.Handled = true;
                new ChangeCarParentDialog((CarObject)SelectedAcObject).ShowDialog();
            }
        }

        private void SpecsInfoBlock_OnMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                e.Handled = true;
                new CarSpecsEditor((CarObject)SelectedAcObject).ShowDialog();
            }
        }
        #endregion
    }
}
