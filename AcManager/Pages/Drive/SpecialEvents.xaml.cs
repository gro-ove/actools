using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Dialogs;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Pages.Drive {
    public partial class SpecialEvents : ILoadableContent {
        public static bool OptionScalableTiles = true;

        public Task LoadAsync(CancellationToken cancellationToken) {
            return SpecialEventsManager.Instance.EnsureLoadedAsync();
        }

        public void Load() {
            SpecialEventsManager.Instance.EnsureLoaded();
        }

        public void Initialize() {
            DataContext = new ViewModel();
            InitializeComponent();
            InputBindings.AddRange(new[] {
                new InputBinding(new DelegateCommand(() => Model.Selected?.GoCommand.Execute(null)), new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(new DelegateCommand(() => Model.Selected?.ViewInExplorerCommand.Execute(null)), new KeyGesture(Key.F, ModifierKeys.Control))
            });
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged, IComparer {
            public AcWrapperCollectionView List { get; }

            private SpecialEventObject _selected;

            [CanBeNull]
            public SpecialEventObject Selected {
                get { return _selected; }
                set {
                    if (Equals(value, _selected)) return;
                    _selected = value;
                    OnPropertyChanged();
                    ValuesStorage.Set(KeySelectedId, value?.Id);
                }
            }

            public ViewModel() {
                List = new AcWrapperCollectionView(SpecialEventsManager.Instance.WrappersAsIList);
                List.CurrentChanged += OnCurrentChanged;
                List.MoveCurrentToIdOrFirst(ValuesStorage.GetString(KeySelectedId));
                List.CustomSort = this;
            }

            private void OnCurrentChanged(object sender, EventArgs e) {
                Selected = List.LoadedCurrent as SpecialEventObject;
                if (Selected != null) {
                    FancyBackgroundManager.Instance.ChangeBackground(Selected.PreviewImage);
                }
            }

            public void Unload() { }

            int IComparer.Compare(object x, object y) {
                return AlphanumComparatorFast.Compare((x as AcItemWrapper)?.Id, (y as AcItemWrapper)?.Id);
            }

            private ICommand _updateProgressCommand;

            public ICommand SyncronizeProgressCommand => _updateProgressCommand ??
                    (_updateProgressCommand = new AsyncCommand(async () => {
                        try {
                            using (var waiting = new WaitingDialog()) {
                                await SpecialEventsManager.Instance.UpdateProgress(waiting, waiting.CancellationToken);
                            }
                        } catch (Exception e) {
                            NonfatalError.Notify("Can’t get challenges progress", e);
                        }
                    }, () => SteamIdHelper.Instance.Value != null));

            private ICommand _syncronizeProgressUsingModuleCommand;

            public ICommand SyncronizeProgressUsingModuleCommand => _syncronizeProgressUsingModuleCommand ??
                    (_syncronizeProgressUsingModuleCommand = new AsyncCommand(async () => {
                        try {
                            using (var waiting = new WaitingDialog()) {
                                await SpecialEventsManager.Instance.UpdateProgressViaModule(waiting, waiting.CancellationToken);
                            }
                        } catch (Exception e) {
                            NonfatalError.Notify("Can’t get challenges progress", e);
                        }
                    }, () => SettingsHolder.Drive.SelectedStarterType == SettingsHolder.DriveSettings.UiModuleStarterType));
        }

        private ScrollViewer _scroll;

        private const string KeyScrollValue = ".SpecialEvents.Scroll";
        private const string KeySelectedId = ".SpecialEvents.Selected";

        private void OnLoaded(object sender, RoutedEventArgs e) {
            _scroll = ListBox.FindVisualChild<ScrollViewer>();
            if (_scroll != null) {
                _scroll.LayoutUpdated += ScrollLayoutUpdated;
            }
        }

        private bool _positionLoaded;

        private async void ScrollLayoutUpdated(object sender, EventArgs e) {
            if (_positionLoaded) return;
            var value = ValuesStorage.GetDoubleNullable(KeyScrollValue) ?? 0d;
            await Task.Delay(10);
            _scroll?.ScrollToHorizontalOffset(OptionScalableTiles ? value / (Equals(_scale, 0d) ? 1d : _scale) : value);
            _positionLoaded = true;
        }

        private void OnScrollSizeChanged(object sender, SizeChangedEventArgs e) {}

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e) {
            if (_scroll == null || !_positionLoaded) return;
            ValuesStorage.Set(KeyScrollValue, OptionScalableTiles ? _scroll.HorizontalOffset * _scale : _scroll.HorizontalOffset);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            Model.Unload();
        }

        private void AssistsMore_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            new AssistsDialog(AssistsViewModel.Instance).ShowDialog();
        }

        private double _scale;

        private void ResizeTiles() {
            if (_tilePanel == null || !OptionScalableTiles) return;

            _positionLoaded = false;

            // Width="195" Height="110"
            var scale = 1d + ((ActualHeight - 640d) / 540d).Saturate();
            if (scale < 1.3) scale = 1d;
            if (Math.Abs(scale - _scale) < 0.2) return;
            
            _tilePanel.ItemWidth = 195d * scale;
            _tilePanel.ItemHeight = 110d * scale;
            _scale = scale;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            ResizeTiles();
        }

        private VirtualizingTilePanel _tilePanel;

        private void TilePanel_OnLoaded(object sender, RoutedEventArgs e) {
            _tilePanel = (VirtualizingTilePanel)sender;
            ResizeTiles();
        }
    }
}
