using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcLog;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.Processes;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Controls.Dialogs {
    public partial class CupInformationDialog {
        public class ViewModel : NotifyPropertyChanged {
            public ICupSupportedObject CupObject { get; }
            public string DisplayAlternativeIds { get; }

            public ViewModel(ICupSupportedObject cupObject) {
                CupObject = cupObject;

                var manager = (cupObject as AcObjectNew)?.GetManager();
                DisplayAlternativeIds = cupObject.CupUpdateInformation?.AlternativeIds?.Select(x => manager?.GetObjectById(x ?? "")?.DisplayName ?? x)
                                                 .JoinToReadableString();
            }
        }

        public ViewModel Model => (ViewModel) DataContext;

        public CupInformationDialog(ICupSupportedObject obj) {
            InitializeComponent();
            DataContext = new ViewModel(obj);

            Buttons = new[] {
                CreateCloseDialogButton("Update", true, false, MessageBoxResult.OK, new AsyncCommand(() =>
                        CupClient.Instance.InstallUpdateAsync(obj.CupContentType, obj.Id, default(CancellationToken)))),
                CloseButton
            };
        }

        private void OnClosing(object sender, CancelEventArgs e) {}

        private bool _click;

        private void OnHyperlinkMouseDown(object sender, MouseButtonEventArgs e) {
            _click = true;
        }

        private void OnHyperlinkMouseUp(object sender, MouseButtonEventArgs e) {
            if (_click) {
                WindowsHelper.ViewInBrowser(Model.CupObject.CupUpdateInformation?.InformationUrl);
            }
        }

        private void OnHyperlinkMouseSelection(object sender, RoutedEventArgs e) {
            _click = false;
        }
    }
}
