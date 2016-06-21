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
using AcManager.Controls.CustomShowroom;
using AcManager.Controls.Helpers;
using AcManager.Controls.Pages.Dialogs;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Pages.Windows;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using StringBasedFilter;
using MenuItem = System.Windows.Controls.MenuItem;

namespace AcManager.Pages.Selected {
    public partial class SelectedCarPage : ILoadableContent, IParametrizedUriContent {
        public class SelectedCarPageViewModel : SelectedAcObjectViewModel<CarObject> {
            public SelectedCarPageViewModel([NotNull] CarObject acObject) : base(acObject) {
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(acObject, nameof(PropertyChanged), Handler);
                FilterTabType = "cars";
            }

            private void Handler(object sender, PropertyChangedEventArgs propertyChangedEventArgs) {
                if (propertyChangedEventArgs.PropertyName == nameof(CarObject.Brand) && SettingsHolder.Content.ChangeBrandIconAutomatically) {
                    var entry = FilesStorage.Instance.GetContentFile(ContentCategory.BrandBadges, SelectedObject.Brand + ".png");
                    if (entry.Exists) {
                        try {
                            FileUtils.RecycleSilent(SelectedObject.BrandBadge);
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
                        FilterDecadeCommand.OnCanExecuteChanged();
                        break;
                }
            }

            #region Filter By
            private RelayCommand _filterClassCommand;

            public RelayCommand FilterClassCommand => _filterClassCommand ?? (_filterClassCommand = new RelayCommand(o => {
                NewFilterTab($"class:{Filter.Encode(SelectedObject.CarClass)}");
            }));

            private RelayCommand _filterBrandCommand;

            public RelayCommand FilterBrandCommand => _filterBrandCommand ?? (_filterBrandCommand = new RelayCommand(o => {
                NewFilterTab($"brand:{Filter.Encode(SelectedObject.Brand)}");
            }));
            #endregion

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

            private RelayCommand _openInCustomShowroomCommand;

            public RelayCommand OpenInCustomShowroomCommand => _openInCustomShowroomCommand ?? (_openInCustomShowroomCommand = new RelayCommand(o => {
                CustomShowroomWrapper.StartAsync(SelectedObject, SelectedObject.SelectedSkin);
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
                    ShowroomPresets = _helper.Create(CarOpenInShowroomDialog.UserPresetableKeyValue, p => {
                        CarOpenInShowroomDialog.RunPreset(p, SelectedObject, SelectedObject.SelectedSkin?.Id);
                    });
                }
            }

            public void InitializeQuickDrivePresets() {
                if (QuickDrivePresets == null) {
                    QuickDrivePresets = _helper.Create(QuickDrive.UserPresetableKeyValue, p => {
                        QuickDrive.RunPreset(p, SelectedObject, SelectedObject.SelectedSkin?.Id);
                    });
                }
            }

            public void InitializeUpdatePreviewsPresets() {
                if (UpdatePreviewsPresets == null) {
                    UpdatePreviewsPresets = _helper.Create(CarUpdatePreviewsDialog.UserPresetableKeyValue, presetFilename => {
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
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception("ID is missing");
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

        private SelectedCarPageViewModel _model;

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException("Can’t find object with provided ID");

            InitializeAcObjectPage(_model = new SelectedCarPageViewModel(_object));
            InputBindings.AddRange(new[] {
                new InputBinding(_model.UpdatePreviewsCommand, new KeyGesture(Key.P, ModifierKeys.Control)),
                new InputBinding(_model.UpdatePreviewsOptionsCommand, new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(_model.UpdatePreviewsManuallyCommand, new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Alt)),

                new InputBinding(_model.DriveCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(_model.DriveOptionsCommand, new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Shift)),

                new InputBinding(_model.OpenInShowroomCommand, new KeyGesture(Key.H, ModifierKeys.Control)),
                new InputBinding(_model.OpenInShowroomOptionsCommand, new KeyGesture(Key.H, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(_model.OpenInCustomShowroomCommand, new KeyGesture(Key.H, ModifierKeys.Alt)),

                new InputBinding(_model.ManageSkinsCommand, new KeyGesture(Key.K, ModifierKeys.Control)),
                new InputBinding(_model.ManageSetupsCommand, new KeyGesture(Key.U, ModifierKeys.Control))
            });
            InitializeComponent();
        }

        #region Skins
        private void SelectedSkinPreview_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left) {
                if (e.ClickCount == 2 && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                    e.Handled = true;
                    CarOpenInShowroomDialog.Run(_model.SelectedObject, _model.SelectedObject.SelectedSkin?.Id);
                } else if (e.ClickCount == 1 && ReferenceEquals(sender, SelectedSkinPreviewImage) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                    e.Handled = true;
                    new ImageViewer(
                        from skin in _model.SelectedObject.Skins where skin.Enabled select skin.PreviewImage,
                        _model.SelectedObject.Skins.Where(x => x.Enabled).IndexOf(_model.SelectedObject.SelectedSkin)
                    ).ShowDialog();
                }
            } else if (e.ChangedButton == MouseButton.Right) {
                e.Handled = true;
                OpenSkinContextMenu((((FrameworkElement)sender).DataContext as AcItemWrapper)?.Value as CarSkinObject);
            }
        }

        private void OpenSkinContextMenu(CarSkinObject skin) {
            if (skin == null) return;

            var contextMenu = new ContextMenu {
                Items = {
                    new MenuItem {
                        Header = $"Skin: {skin.DisplayName?.Replace("_", "__") ?? "?"}",
                        StaysOpenOnClick = true
                    }
                }
            };

            var item = new MenuItem { Header = "Open In Showroom", InputGestureText = "Ctrl+H" };
            item.Click += (sender, args) => CarOpenInShowroomDialog.Run(_model.SelectedObject, skin.Id);
            contextMenu.Items.Add(item);

            item = new MenuItem { Header = "Open In Custom Showroom", InputGestureText = "Alt+H" };
            item.Click += (sender, args) => CustomShowroomWrapper.StartAsync(_model.SelectedObject, skin);
            contextMenu.Items.Add(item);

            contextMenu.Items.Add(new MenuItem {
                Header = "Folder",
                Command = skin.ViewInExplorerCommand
            });

            contextMenu.Items.Add(new Separator());

            item = new MenuItem { Header = "Update Preview" };
            item.Click += (sender, args) => new CarUpdatePreviewsDialog(_model.SelectedObject, new[] { skin.Id },
                    SelectedCarPageViewModel.GetAutoUpdatePreviewsDialogMode()).ShowDialog();
            contextMenu.Items.Add(item);

            item = new MenuItem { Header = "Change Livery" };
            item.Click += (sender, args) => new LiveryIconEditor(skin).ShowDialog();
            contextMenu.Items.Add(item);

            contextMenu.Items.Add(new Separator());

            contextMenu.Items.Add(new MenuItem {
                Header = "Delete Skin",
                ToolTip = "Skin will be moved to the Recycle Bin",
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
