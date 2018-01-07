using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using JetBrains.Annotations;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Windows;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using Microsoft.Win32;
using StringBasedFilter;

namespace AcManager.Pages.Lists {
    public partial class TrackSkinsListPage : IParametrizedUriContent, ILoadableContent {
        public void OnUri(Uri uri) {
            _trackId = uri.GetQueryParam("TrackId");
            _filter = uri.GetQueryParam("Filter");
            if (_trackId == null) {
                throw new Exception(ToolsStrings.Common_IdIsMissing);
            }
        }

        private string _trackId;
        private TrackObject _track;
        private string _filter;

        public async Task LoadAsync(CancellationToken cancellationToken) {
            if (_track == null) {
                _track = await TracksManager.Instance.GetByIdAsync(_trackId);
                if (_track == null) throw new Exception("Can’t find track with provided ID");
            }

            await _track.SkinsManager.EnsureLoadedAsync();
        }

        public void Load() {
            if (_track == null) {
                _track = TracksManager.Instance.GetById(_trackId);
                if (_track == null) throw new Exception("Can’t find track with provided ID");
            }

            _track.SkinsManager.EnsureLoaded();
        }

        public void Initialize() {
            DataContext = new ViewModel(_track, string.IsNullOrEmpty(_filter) ? null : Filter.Create(TrackSkinObjectTester.Instance, _filter));
            InitializeComponent();
        }

        public TrackSkinsListPage([NotNull] TrackObject track, string filter = null) {
            _track = track;
            _filter = filter;
        }

        public TrackSkinsListPage() { }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Load();
            if (((ViewModel)DataContext).MainList.Count > 20){
                FancyHints.MultiSelectionMode.Trigger();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Unload();
        }

        public class ViewModel : AcListPageViewModel<TrackSkinObject> {
            public TrackObject SelectedTrack { get; private set; }

            public ViewModel([NotNull] TrackObject track, IFilter<TrackSkinObject> listFilter)
                    : base(track.SkinsManager, listFilter) {
                SelectedTrack = track;
            }

            protected override string GetSubject() {
                return AppStrings.List_Skins;
            }
        }

        public static void Open(TrackObject track) {
            if (!(Application.Current?.MainWindow is MainWindow main)
                    || Keyboard.Modifiers == ModifierKeys.Control && !User32.IsKeyPressed(System.Windows.Forms.Keys.K)
                    || SettingsHolder.Interface.SkinsSetupsNewWindow) {
                TrackSkinsDialog.Show(track);
            } else {
                main.OpenSubGroup("track skins", $"Skins for {track.DisplayNameWithoutCount}",
                        UriExtension.Create("/Pages/Lists/TrackSkinsListPage.xaml?TrackId={0}", track.Id), FilterHints.TrackSkins, 3);
            }
        }

        #region Batch actions
        protected override IEnumerable<BatchAction> GetBatchActions() {
            return CommonBatchActions.GetDefaultSet<TrackSkinObject>().Concat(new BatchAction[] {
                BatchAction_PackSkins.Instance,
            });
        }

        public class BatchAction_PackSkins : CommonBatchActions.BatchAction_Pack<TrackSkinObject> {
            public static readonly BatchAction_PackSkins Instance = new BatchAction_PackSkins();
            public BatchAction_PackSkins() : base("Batch.PackTrackSkins") {}

            #region Properies
            private bool _jsgmeCompatible = ValuesStorage.GetBool("_ba.packTrackSkins.jsgme", true);
            public bool JsgmeCompatible {
                get => _jsgmeCompatible;
                set {
                    if (Equals(value, _jsgmeCompatible)) return;
                    _jsgmeCompatible = value;
                    ValuesStorage.Set("_ba.packTrackSkins.jsgme", value);
                    OnPropertyChanged();
                }
            }

            private string _includeJsgme = ValuesStorage.GetString("_ba.packTrackSkins.includeJsgme");
            public string IncludeJsgme {
                get => _includeJsgme;
                set {
                    if (Equals(value, _includeJsgme)) return;
                    _includeJsgme = value;
                    ValuesStorage.Set("_ba.packTrackSkins.includeJsgme", value);
                    OnPropertyChanged();
                }
            }

            private DelegateCommand _jsgmeChangeCommand;

            public DelegateCommand JsgmeChangeCommand => _jsgmeChangeCommand ?? (_jsgmeChangeCommand = new DelegateCommand(() => {
                var dialog = new OpenFileDialog {
                    Filter = "JSGME|JSGME.exe|Applications (*.exe)|*.exe|All files (*.*)|*.*",
                    Title = "Select JSGME executable"
                };

                if (dialog.ShowDialog() == true) {
                    IncludeJsgme = dialog.FileName;
                }
            }));

            private DelegateCommand _jsgmeClearCommand;

            public DelegateCommand JsgmeClearCommand => _jsgmeClearCommand ?? (_jsgmeClearCommand = new DelegateCommand(() => {
                IncludeJsgme = null;
            }));
            #endregion

            protected override AcCommonObject.AcCommonObjectPackerParams GetParams() {
                return new TrackSkinObject.TrackSkinPackerParams {
                    JsgmeCompatible = JsgmeCompatible,
                    IncludeJsgme = IncludeJsgme
                };
            }
        }
        #endregion

        protected override void OnItemDoubleClick(AcObjectNew obj) {
            if (obj is TrackSkinObject skin) {
                var track = TracksManager.Instance.GetById(skin.TrackId);
                if (track != null) {
                    /*QuickDrive.Show(track, skin.Id);*/
                }
            }
        }
    }
}
