using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Tools.Objects;

namespace AcManager.Controls.Dialogs {
    public enum UserChampionshipIntroMode {
        Intro, InstallationPreview, InstallationAlreadyExistingPreview
    }

    public partial class UserChampionshipIntro {
        public IUserChampionshipInformation UserChampionshipObject { get; }

        public UserChampionshipIntroMode DialogMode { get; }

        public string ExistingName { get; }

        public UserChampionshipIntro(IUserChampionshipInformation userChampionshipObject, UserChampionshipIntroMode mode = UserChampionshipIntroMode.Intro,
                string existingName = null) {
            DataContext = this;
            UserChampionshipObject = userChampionshipObject;
            DialogMode = mode;

            if (mode != UserChampionshipIntroMode.Intro) {
                CloseButton.Visibility = Visibility.Collapsed;
            }

            ExistingName = existingName;
            Title = UserChampionshipObject.Name;
            InitializeComponent();
            Buttons = new Button[] { };
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                Close();
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape || e.Key == Key.Back || e.Key == Key.BrowserBack ||
                    e.Key == Key.Q || e.Key == Key.W && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                Close();
            }
        }

        private void CloseButton_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            Close();
        }
    }
}
