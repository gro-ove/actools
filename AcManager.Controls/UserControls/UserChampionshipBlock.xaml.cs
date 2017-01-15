using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls.Dialogs;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Controls.UserControls {
    public partial class UserChampionshipBlock {
        public UserChampionshipBlock() {
            InitializeComponent();
        }

        public object ButtonPlaceholder {
            get { return ButtonPresenter.Content; }
            set { ButtonPresenter.Content = value; }
        }

        public UserChampionshipObject KunosCareerObject => DataContext as UserChampionshipObject;

        private void Information_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            var career = KunosCareerObject;
            if (career == null) return;
            
            new UserChampionshipIntro(career).ShowDialog();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            var career = KunosCareerObject;
            if (career == null) return;

            InformationIcon.Data = TryFindResource(@"InformationIconData") as Geometry;
        }
    }
}
