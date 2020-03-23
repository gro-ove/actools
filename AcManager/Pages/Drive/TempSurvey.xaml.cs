using System.Windows;
using AcManager.Pages.Windows;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Pages.Drive {
    public partial class TempSurvey {
        public TempSurvey() {
            InitializeComponent();
        }

        private void OnButtonClick(object sender, RoutedEventArgs e) {
            Stored.Get<bool>("surveyHide").Value = !Stored.Get<bool>("surveyHide").Value;
            if (Application.Current.MainWindow is MainWindow window) {
                window.ShortSurveyLink.IsShown = false;
            }
        }
    }
}