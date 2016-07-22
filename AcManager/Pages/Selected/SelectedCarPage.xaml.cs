using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Annotations;
using AcManager.Controls;
using AcManager.Controls.CustomShowroom;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Tools;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.AcdFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using StringBasedFilter;
using MenuItem = System.Windows.Controls.MenuItem;

namespace AcManager.Pages.Selected {
    public partial class SelectedCarPage : ILoadableContent, IParametrizedUriContent {
        public class ViewModel : SelectedAcObjectViewModel<CarObject> {
            public ViewModel([NotNull] CarObject acObject) : base(acObject) {
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(acObject, nameof(PropertyChanged), Handler);
            }

            private void Handler(object sender, PropertyChangedEventArgs propertyChangedEventArgs) {
                if (propertyChangedEventArgs.PropertyName == nameof(CarObject.Brand) && SettingsHolder.Content.ChangeBrandIconAutomatically) {
                    var entry = FilesStorage.Instance.GetContentFile(ContentCategory.BrandBadges, SelectedObject.Brand + @".png");
                    if (entry.Exists) {
                        try {
                            FileUtils.Recycle(SelectedObject.BrandBadge);
                            File.Copy(entry.Filename, SelectedObject.BrandBadge);
                        } catch (Exception e) {
                            Logging.Warning("Can’t change brand badge: " + e);
                        }
                    }
                }
            }

            public override void Load() {
                base.Load();
                SelectedObject.PropertyChanged += SelectedObject_PropertyChanged;
                // new LiveryIconEditor(SelectedObject.SelectedSkin).ShowDialog();
            }

            public override void Unload() {
                base.Unload();
                SelectedObject.PropertyChanged -= SelectedObject_PropertyChanged;
                _helper.Dispose();
            }

            private void SelectedObject_PropertyChanged(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(AcCommonObject.Year):
                        FilterCommand.OnCanExecuteChanged();
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

            #region Open In Showroom
            private RelayCommand _openInShowroomCommand;

            public RelayCommand OpenInShowroomCommand => _openInShowroomCommand ?? (_openInShowroomCommand = new RelayCommand(o => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                    OpenInCustomShowroomCommand.Execute(o);
                    return;
                }

                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                        !CarOpenInShowroomDialog.Run(SelectedObject, SelectedObject.SelectedSkin?.Id)) {
                    OpenInShowroomOptionsCommand.Execute(null);
                }
            }, o => SelectedObject.Enabled && SelectedObject.SelectedSkin != null));

            private RelayCommand _openInShowroomOptionsCommand;

            public RelayCommand OpenInShowroomOptionsCommand => _openInShowroomOptionsCommand ?? (_openInShowroomOptionsCommand = new RelayCommand(o => {
                new CarOpenInShowroomDialog(SelectedObject, SelectedObject.SelectedSkin?.Id).ShowDialog();
            }, o => SelectedObject.Enabled && SelectedObject.SelectedSkin != null));

            private AsyncCommand _openInCustomShowroomCommand;

            public AsyncCommand OpenInCustomShowroomCommand => _openInCustomShowroomCommand ?? (_openInCustomShowroomCommand = new AsyncCommand(o => {
                var type = o as CustomShowroomMode?;
                return type.HasValue
                        ? CustomShowroomWrapper.StartAsync(type.Value, SelectedObject, SelectedObject.SelectedSkin)
                        : CustomShowroomWrapper.StartAsync(SelectedObject, SelectedObject.SelectedSkin);
            }));

            private RelayCommand _driveCommand;

            public RelayCommand DriveCommand => _driveCommand ?? (_driveCommand = new RelayCommand(o => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                        !QuickDrive.Run(SelectedObject, SelectedObject.SelectedSkin?.Id)) {
                    DriveOptionsCommand.Execute(null);
                }
            }, o => SelectedObject.Enabled));

            private RelayCommand _driveOptionsCommand;

            public RelayCommand DriveOptionsCommand => _driveOptionsCommand ?? (_driveOptionsCommand = new RelayCommand(o => {
                QuickDrive.Show(SelectedObject, SelectedObject.SelectedSkin?.Id);
            }, o => SelectedObject.Enabled));
            #endregion

            #region Auto-Update Previews
            private ICommand _updatePreviewsCommand;

            public ICommand UpdatePreviewsCommand => _updatePreviewsCommand ?? (_updatePreviewsCommand = new RelayCommand(o => {
                new CarUpdatePreviewsDialog(SelectedObject, GetAutoUpdatePreviewsDialogMode()).ShowDialog();
            }, o => SelectedObject.Enabled));

            private ICommand _updatePreviewsManuallyCommand;

            public ICommand UpdatePreviewsManuallyCommand => _updatePreviewsManuallyCommand ?? (_updatePreviewsManuallyCommand = new RelayCommand(o => {
                new CarUpdatePreviewsDialog(SelectedObject, CarUpdatePreviewsDialog.DialogMode.StartManual).ShowDialog();
            }, o => SelectedObject.Enabled));

            private ICommand _updatePreviewsOptionsCommand;

            public ICommand UpdatePreviewsOptionsCommand => _updatePreviewsOptionsCommand ?? (_updatePreviewsOptionsCommand = new RelayCommand(o => {
                new CarUpdatePreviewsDialog(SelectedObject, CarUpdatePreviewsDialog.DialogMode.Options).ShowDialog();
            }, o => SelectedObject.Enabled));

            public static CarUpdatePreviewsDialog.DialogMode GetAutoUpdatePreviewsDialogMode() {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return CarUpdatePreviewsDialog.DialogMode.Options;
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) return CarUpdatePreviewsDialog.DialogMode.StartManual;
                return CarUpdatePreviewsDialog.DialogMode.Start;
            }
            #endregion

            #region Presets
            public ObservableCollection<MenuItem> ShowroomPresets {
                get { return _showroomPresets; }
                private set {
                    if (Equals(value, _showroomPresets)) return;
                    _showroomPresets = value;
                    OnPropertyChanged();
                }
            }

            public ObservableCollection<MenuItem> UpdatePreviewsPresets {
                get { return _updatePreviewsPresets; }
                private set {
                    if (Equals(value, _updatePreviewsPresets)) return;
                    _updatePreviewsPresets = value;
                    OnPropertyChanged();
                }
            }

            public ObservableCollection<MenuItem> QuickDrivePresets {
                get { return _quickDrivePresets; }
                private set {
                    if (Equals(value, _quickDrivePresets)) return;
                    _quickDrivePresets = value;
                    OnPropertyChanged();
                }
            }

            private ObservableCollection<MenuItem> _showroomPresets, _updatePreviewsPresets, _quickDrivePresets;
            private readonly PresetsMenuHelper _helper = new PresetsMenuHelper();

            public void InitializeShowroomPresets() {
                if (ShowroomPresets == null) {
                    ShowroomPresets = _helper.Create(CarOpenInShowroomDialog.PresetableKeyValue, p => {
                        CarOpenInShowroomDialog.RunPreset(p, SelectedObject, SelectedObject.SelectedSkin?.Id);
                    });
                }
            }

            public void InitializeQuickDrivePresets() {
                if (QuickDrivePresets == null) {
                    QuickDrivePresets = _helper.Create(QuickDrive.PresetableKeyValue, p => {
                        QuickDrive.RunPreset(p, SelectedObject, SelectedObject.SelectedSkin?.Id);
                    });
                }
            }

            public void InitializeUpdatePreviewsPresets() {
                if (UpdatePreviewsPresets == null) {
                    UpdatePreviewsPresets = _helper.Create(CarUpdatePreviewsDialog.PresetableKeyValue, presetFilename => {
                        new CarUpdatePreviewsDialog(SelectedObject, GetAutoUpdatePreviewsDialogMode(), presetFilename).ShowDialog();
                    });
                }
            }
            #endregion

            private RelayCommand _manageSkinsCommand;

            public RelayCommand ManageSkinsCommand => _manageSkinsCommand ?? (_manageSkinsCommand = new RelayCommand(o => {
                new CarSkinsDialog(SelectedObject) {
                    ShowInTaskbar = false
                }.ShowDialogWithoutBlocking();
            }));

            private RelayCommand _manageSetupsCommand;

            public RelayCommand ManageSetupsCommand => _manageSetupsCommand ?? (_manageSetupsCommand = new RelayCommand(o => {
                new CarSetupsDialog(SelectedObject) {
                    ShowInTaskbar = false
                }.ShowDialogWithoutBlocking();
            }));

            private string DataDirectory => Path.Combine(SelectedObject.Location, "data");

            private RelayCommand _readDataCommand;

            public RelayCommand ReadDataCommand => _readDataCommand ?? (_readDataCommand = new RelayCommand(o => {
                var source = Path.Combine(SelectedObject.Location, "data.a" + "cd");
                try {
                    var destination = FileUtils.EnsureUnique(DataDirectory);
                    Acd.FromFile(source).ExportDirectory(destination);
                    WindowsHelper.ViewDirectory(destination);
                } catch (Exception e) {
                    NonfatalError.Notify(ToolsStrings.Common_CannotReadData, e);
                }
            }, o => SettingsHolder.Common.MsMode && SelectedObject.AcdData.IsPacked));

            private RelayCommand _packDataCommand;

            public RelayCommand PackDataCommand => _packDataCommand ?? (_packDataCommand = new RelayCommand(o => {
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
            }, o => SettingsHolder.Common.DeveloperMode && Directory.Exists(DataDirectory)));

            private AsyncCommand _replaceSoundCommand;

            public AsyncCommand ReplaceSoundCommand => _replaceSoundCommand ?? (_replaceSoundCommand = new AsyncCommand(async o => {
                var donor = SelectCarDialog.Show();
                if (donor == null) return;

                try {
                    using (new WaitingDialog()) {
                        var guids = Path.Combine(donor.Location, @"sfx", @"GUIDs.txt");
                        var soundbank = Path.Combine(donor.Location, @"sfx", $"{donor.Id}.bank");

                        var newGuilds = Path.Combine(SelectedObject.Location, @"sfx", @"GUIDs.txt");
                        var newSoundbank = Path.Combine(SelectedObject.Location, @"sfx", $"{SelectedObject.Id}.bank");

                        await Task.Run(() => {
                            var destinations = new[] { newGuilds, newSoundbank }.Where(File.Exists).Select(x => new {
                                Original = x,
                                Backup = FileUtils.EnsureUnique($"{x}.bak")
                            }).ToList();

                            foreach (var oldFile in destinations) {
                                File.Move(oldFile.Original, oldFile.Backup);
                            }

                            try {
                                if (File.Exists(guids) && File.Exists(soundbank)) {
                                    File.Copy(soundbank, newSoundbank);
                                    File.WriteAllText(newGuilds, File.ReadAllText(guids).Replace(donor.Id, SelectedObject.Id));
                                } else if (File.Exists(soundbank) && donor.Author == AcCommonObject.AuthorKunos) {
                                    File.Copy(soundbank, newSoundbank);
                                    File.WriteAllText(newGuilds, File.ReadAllLines(FileUtils.GetSfxGuidsFilename(AcRootDirectory.Instance.RequireValue))
                                                                     .Where(x => !x.Contains(@"} bank:/") || x.Contains(@"} bank:/common") ||
                                                                             x.EndsWith(@"} bank:/" + donor.Id))
                                                                     .Where(x => !x.Contains(@"} event:/") || x.Contains(@"} event:/cars/" + donor.Id + @"/"))
                                                                     .JoinToString(Environment.NewLine).Replace(donor.Id, SelectedObject.Id));
                                } else {
                                    throw new InformativeException(AppStrings.Car_ReplaceSound_WrongCar, AppStrings.Car_ReplaceSound_WrongCar_Commentary);
                                }
                            } catch (Exception) {
                                foreach (var oldFile in destinations) {
                                    if (File.Exists(oldFile.Original)) {
                                        File.Delete(oldFile.Original);
                                    }
                                    File.Move(oldFile.Backup, oldFile.Original);
                                }
                                throw;
                            }
                            
                            FileUtils.Recycle(destinations.Select(x => x.Backup).ToArray());
                        });
                    }
                } catch (Exception e) {
                    NonfatalError.Notify(AppStrings.Car_ReplaceSound_CannotReplace, AppStrings.Car_ReplaceSound_CannotReplace_Commentary, e);
                }
            }, o => SettingsHolder.Common.MsMode));
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

        private ViewModel _model;

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException(AppStrings.Common_CannotFindObjectById);

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
            InitializeComponent();

            if (SettingsHolder.CustomShowroom.LiteByDefault) {
                LiteCustomShowroomMenuItem.InputGestureText = @"Alt+H";
                FancyCustomShowroomMenuItem.InputGestureText = @"Ctrl+Alt+H";
            } else {
                LiteCustomShowroomMenuItem.InputGestureText = @"Ctrl+Alt+H";
                FancyCustomShowroomMenuItem.InputGestureText = @"Alt+H";
            }
        }

        #region Skins
        private void SelectedSkinPreview_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount == 2 && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                e.Handled = true;
                CarOpenInShowroomDialog.Run(_model.SelectedObject, _model.SelectedObject.SelectedSkin?.Id);
            } else if (e.ClickCount == 1 && ReferenceEquals(sender, SelectedSkinPreviewImage) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                e.Handled = true;
                new ImageViewer(
                        from skin in _model.SelectedObject.Skins where skin.Enabled select skin.PreviewImage,
                        _model.SelectedObject.Skins.Where(x => x.Enabled).IndexOf(_model.SelectedObject.SelectedSkin),
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
            item.Click += (sender, args) => new CarUpdatePreviewsDialog(_model.SelectedObject, new[] { skin.Id },
                    ViewModel.GetAutoUpdatePreviewsDialogMode()).ShowDialog();
            contextMenu.Items.Add(item);

            contextMenu.Items.Add(new Separator());

            item = new MenuItem { Header = AppStrings.Toolbar_ChangeLivery };
            item.Click += (sender, args) => new LiveryIconEditor(skin).ShowDialog();
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
        private void ToolbarButtonShowroom_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeShowroomPresets();
        }

        private void ToolbarButtonQuickDrive_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeQuickDrivePresets();
        }

        private void ToolbarButtonUpdatePreviews_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeUpdatePreviewsPresets();
        }
        #endregion

        #region Icons & Specs
        private void AcObjectBase_IconMouseDown(object sender, MouseButtonEventArgs e) {
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
