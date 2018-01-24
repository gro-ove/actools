using System;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.UserControls;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Pages.Dialogs {
    public partial class FindInformationDialog {
        private ViewModel Model => (ViewModel)DataContext;

        public FindInformationDialog(AcJsonObjectNew obj) {
            DataContext = new ViewModel(obj);
            InitializeComponent();

            Buttons = new[] {
                CreateExtraDialogButton(ControlsStrings.FindInformation_SaveAndClose, new CombinedCommand(Model.SaveCommand, CloseCommand)),
                CloseButton
            };

            WebBrowser.SetScriptProvider(new ScriptProvider(Model));
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
        public class ScriptProvider : ScriptProviderBase {
            private readonly ViewModel _model;

            public ScriptProvider(ViewModel model) {
                _model = model;
            }

            public void Update(string selected) {
                Sync(() => {
                    _model.SelectedText = Regex.Replace(selected ?? "", @"\[(?:\d+|citation needed)\]", "").Trim();
                });
            }
        }

        public class ViewModel : NotifyPropertyChanged {
            public AcJsonObjectNew SelectedObject { get; }

            public string StartPage { get; }

            private string _selectedText = "";

            [NotNull]
            public string SelectedText {
                get { return _selectedText; }
                set {
                    if (Equals(value, _selectedText)) return;
                    _selectedText = value;
                    OnPropertyChanged();
                    _saveCommand?.RaiseCanExecuteChanged();
                    UpdateSaveLabel();
                }
            }

            private string _saveLabel;

            public string SaveLabel {
                get { return _saveLabel; }
                set {
                    if (Equals(value, _saveLabel)) return;
                    _saveLabel = value;
                    OnPropertyChanged();
                }
            }

            public ViewModel(AcJsonObjectNew selectedObject) {
                SelectedObject = selectedObject;
                StartPage = GetSearchAddress(SelectedObject);
            }

            private void UpdateSaveLabel() {
                if (string.IsNullOrEmpty(SelectedText)) {
                    SaveLabel = AppStrings.Toolbar_Save;
                    return;
                }

                if (Regex.IsMatch(SelectedText, @"^(1[89]\d\d|20[012]\d)$")) {
                    SaveLabel = ControlsStrings.FindInformation_SaveAsYear;
                    return;
                }

                var key = SelectedText.ToLower();
                if (DataProvider.Instance.TagCountries.GetValueOrDefault(key) != null || DataProvider.Instance.Countries.GetValueOrDefault(key) != null) {
                    SaveLabel = ControlsStrings.FindInformation_SaveAsCountry;
                    return;
                }

                SaveLabel = ControlsStrings.FindInformation_SaveAsDescription;
            }

            private void Save() {
                if (Regex.IsMatch(SelectedText, @"^(1[89]\d\d|20[012]\d)$")) {
                    var year = FlexibleParser.ParseInt(SelectedText);
                    SelectedObject.Year = year;
                    return;
                }

                var key = SelectedText.ToLower();
                var country = DataProvider.Instance.TagCountries.GetValueOrDefault(key) ?? DataProvider.Instance.Countries.GetValueOrDefault(key);
                if (country != null) {
                    SelectedObject.Country = country;
                    return;
                }

                SelectedObject.Description = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ? (
                        Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) ?
                                SelectedText + Environment.NewLine.RepeatString(2) + SelectedObject.Description :
                                SelectedObject.Description + Environment.NewLine.RepeatString(2) + SelectedText
                        ).Trim() : SelectedText;
            }

            private DelegateCommand _saveCommand;

            public DelegateCommand SaveCommand => _saveCommand ?? (_saveCommand = new DelegateCommand(Save, () => !string.IsNullOrEmpty(SelectedText)));
        }

        private static string GetSearchAddress(AcCommonObject obj) {
            return SettingsHolder.Content.SearchEngine?.GetUri(obj.Name ?? obj.Id, true) ??
                    $"https://duckduckgo.com/?q=site%3Awikipedia.org+{Uri.EscapeDataString(obj.Name ?? obj.Id)}&ia=web";
        }

        private void OnPageLoaded(object sender, PageLoadedEventArgs e) {
            WebBrowser.Execute(@"
document.addEventListener('mouseup', function(){
    window.external.Update(window.getSelection().toString());
}, false);

document.addEventListener('mousedown', function(e){
    if (e.target.getAttribute('target') == '_blank'){
        e.target.setAttribute('target', '_parent');
    }
}, false);");
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;
            }
        }
    }
}
