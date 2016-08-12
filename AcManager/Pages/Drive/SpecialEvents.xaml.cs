using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Dialogs;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

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
                new InputBinding(new RelayCommand(o => Model.Selected?.GoCommand.Execute(o)), new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(new RelayCommand(o => Model.Selected?.ViewInExplorerCommand.Execute(o)), new KeyGesture(Key.F, ModifierKeys.Control))
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

            public ICommand SyncronizeProgressCommand => _updateProgressCommand ?? (_updateProgressCommand = new AsyncCommand(async o => {
                try {
                    using (var waiting = new WaitingDialog()) {
                        await SpecialEventsManager.Instance.UpdateProgress(waiting, waiting.CancellationToken);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t get challenges progress", e);
                }
            }, o => SteamIdHelper.Instance.Value != null));

            private ICommand _syncronizeProgressUsingModuleCommand;

            public ICommand SyncronizeProgressUsingModuleCommand
                => _syncronizeProgressUsingModuleCommand ?? (_syncronizeProgressUsingModuleCommand = new AsyncCommand(async o => {
                    using (var waiting = new WaitingDialog()) {
                        await SpecialEventsManager.Instance.UpdateProgressViaModule(waiting, waiting.CancellationToken);
                    }
                }, o => SettingsHolder.Drive.SelectedStarterType == SettingsHolder.DriveSettings.UiModuleStarterType));
        }

        private ScrollViewer _scrollViewer;

        private const string KeyScrollValue = ".SpecialEvents.Scroll";
        private const string KeySelectedId = ".SpecialEvents.Selected";

        private void OnLoaded(object sender, RoutedEventArgs e) {
            _scrollViewer = ListBox.FindVisualChild<ScrollViewer>();
            if (_scrollViewer != null) {
                _scrollViewer.LayoutUpdated += _scrollViewer_LayoutUpdated;
            }
        }

        private bool _positionLoaded;

        private void _scrollViewer_LayoutUpdated(object sender, EventArgs e) {
            if (_positionLoaded) return;
            var value = ValuesStorage.GetDoubleNullable(KeyScrollValue) ?? 0d;
            _scrollViewer?.ScrollToHorizontalOffset(value);
            _positionLoaded = true;
        }

        private void OnScrollSizeChanged(object sender, SizeChangedEventArgs e) {}

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e) {
            if (_scrollViewer == null || !_positionLoaded) return;
            ValuesStorage.Set(KeyScrollValue, _scrollViewer.HorizontalOffset);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            Model.Unload();
        }

        private void AssistsMore_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            new AssistsDialog(AssistsViewModel.Instance).ShowDialog();
        }

        private double _previousScale;

        private void ResizeTiles() {
            if (_tilePanel == null || !OptionScalableTiles) return;

            // Width="195" Height="110"
            var scale = 1d + ((ActualHeight - 640d) / 540d).Saturate();
            if (scale < 1.3) scale = 1d;
            if (Math.Abs(scale - _previousScale) < 0.2) return;
            if (!Equals(_previousScale, 0d)) {
                _scrollViewer.ScrollToHorizontalOffset(_scrollViewer.HorizontalOffset * scale / _previousScale);
            }

            _tilePanel.ItemWidth = 195d * scale;
            _tilePanel.ItemHeight = 110d * scale;
            _previousScale = scale;
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
