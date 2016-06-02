using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls.Pages.Dialogs;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;

namespace AcManager.Controls.UserControls {
    public partial class KunosCareerBlock {
        public KunosCareerBlock() {
            InitializeComponent();
        }

        public object ButtonPlaceholder {
            get { return ButtonPresenter.Content; }
            set { ButtonPresenter.Content = value; }
        }

        public KunosCareerObject KunosCareerObject => DataContext as KunosCareerObject;

        private void Information_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            var career = KunosCareerObject;
            if (career == null) return;

            if (File.Exists(career.StartVideo)) {
                if (VideoViewer.IsSupported()) {
                    var videoViewer = new VideoViewer(career.StartVideo, career.Name);
                    videoViewer.ShowDialog();
                } else {
                    NonfatalError.Notify("Can’t play video", "Go to settings, download and enable VLC addon.");
                }
            }
            
            new KunosCareerIntro(career).ShowDialog();
        }

        private void KunosCareerBlock_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            var career = KunosCareerObject;
            if (career == null) return;

            InformationIcon.Data = FindResource(File.Exists(career.StartVideo) ? "MovieIconData" : "InformationIconData") as Geometry;
        }
    }
}
