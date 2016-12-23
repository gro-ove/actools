using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Managers.Online;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Dialogs {
    public partial class OnlineAddManuallyDialog {
        private readonly string _key;

        private ViewModel Model => (ViewModel)DataContext;

        public OnlineAddManuallyDialog(string key, IReadOnlyList<ServerInformationComplete> informations) {
            _key = key;
            DataContext = new ViewModel(informations);
            InitializeComponent();
            Buttons = new[] { informations.Count > 0 ? OkButton : null, CancelButton }.NonNull();

            List.SelectedItems.Clear();
            foreach (var information in informations) {
                List.SelectedItems.Add(information);
            }
        }

        private void OnClosing(object sender, CancelEventArgs e) {
            if (!IsResultOk) return;

            foreach (var result in List.SelectedItems.OfType<ServerInformationComplete>()) {
                FileBasedOnlineSources.AddToList(_key, result);
            }
        }

        public class ViewModel : NotifyPropertyChanged {
            public IReadOnlyList<ServerInformationComplete> Informations { get; }

            public ViewModel(IReadOnlyList<ServerInformationComplete> informations) {
                Informations = informations;
            }
        }
    }
}
