using System;
using System.Threading.Tasks;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Pages.Dialogs {
    public partial class TrackSkinsDialog {
        private class ViewModel {
            [NotNull]
            public TrackObject SelectedTrack { get; }

            public Uri ListUri => UriExtension.Create("/Pages/Lists/TrackSkinsListPage.xaml?TrackId={0}", SelectedTrack.Id);

            public ViewModel([NotNull] TrackObject track) {
                SelectedTrack = track;
            }
        }

        private ViewModel Model => (ViewModel) DataContext;

        private TrackSkinsDialog([NotNull] TrackObject track) {
            if (track == null) throw new ArgumentNullException(nameof(track));

            DataContext = new ViewModel(track);

            DefaultContentSource = Model.ListUri;
            MenuLinkGroups.Add(new LinkGroupFilterable {
                DisplayName = AppStrings.Main_Skins,
                Source = Model.ListUri
            });

            InitializeComponent();
        }

        public static void Show([NotNull] TrackObject track) {
            new TrackSkinsDialog(track) {
                ShowInTaskbar = false
            }.ShowDialogWithoutBlocking();
        }

        private void OnInitialized(object sender, EventArgs e) {
            if (Model?.SelectedTrack == null) return;
            Model.SelectedTrack.AcObjectOutdated += SelectedTrack_AcObjectOutdated;
        }

        private void OnClosed(object sender, EventArgs e) {
            if (Model?.SelectedTrack == null) return;
            Model.SelectedTrack.AcObjectOutdated -= SelectedTrack_AcObjectOutdated;
        }

        private async void SelectedTrack_AcObjectOutdated(object sender, EventArgs e) {
            Hide();

            await Task.Delay(10);
            Close();
        }
    }
}
