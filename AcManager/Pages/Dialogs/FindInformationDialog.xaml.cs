using System;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using System.Windows.Input;
using AcManager.Controls.UserControls;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Pages.Dialogs {
    public partial class FindInformationDialog {
        private FindInformationViewModel Model => (FindInformationViewModel)DataContext;

        public FindInformationDialog(AcJsonObjectNew obj) {
            DataContext = new FindInformationViewModel(obj);
            InitializeComponent();

            Buttons = new[] {
                CreateExtraDialogButton("Save & Close", new CombinedCommand(Model.SaveCommand, CloseCommand)),
                CloseButton
            };

            WebBrowser.SetScriptProvider(new ScriptProvider(Model));
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        [ComVisible(true)]
        public class ScriptProvider : BaseScriptProvider {
            private readonly FindInformationViewModel _model;

            public ScriptProvider(FindInformationViewModel model) {
                _model = model;
            }

            public void Update(string selected) {
                _model.SelectedText = Regex.Replace(selected ?? "", @"\[(?:\d+|citation needed)\]", "").Trim();
            }
        }

        public class FindInformationViewModel : NotifyPropertyChanged {
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
                    SaveCommand.OnCanExecuteChanged();
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

            public FindInformationViewModel(AcJsonObjectNew selectedObject) {
                SelectedObject = selectedObject;
                StartPage = GetMapAddress(SelectedObject);
            }

            private void UpdateSaveLabel() {
                if (string.IsNullOrEmpty(SelectedText)) {
                    SaveLabel = "Save";
                    return;
                }

                if (Regex.IsMatch(SelectedText, @"^(1[89]\d\d|20[012]\d)$")) {
                    SaveLabel = "Save as Year";
                    return;
                }

                var key = SelectedText.ToLower();
                if (DataProvider.Instance.TagCountries.GetValueOrDefault(key) != null || DataProvider.Instance.Countries.GetValueOrDefault(key) != null) {
                    SaveLabel = "Save as Country";
                    return;
                }

                SaveLabel = "Save as Description";
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
                                SelectedText + "\n\n" + SelectedObject.Description :
                                SelectedObject.Description + "\n\n" + SelectedText
                        ).Trim() : SelectedText;
            }

            private RelayCommand _saveCommand;

            public RelayCommand SaveCommand => _saveCommand ?? (_saveCommand = new RelayCommand(o => {
                Save();
            }, o => !string.IsNullOrEmpty(SelectedText)));
        }
        
        private static string GetMapAddress(AcCommonObject obj) {
            return SettingsHolder.Content.SearchEngine?.GetUri(obj.DisplayName) ??
                    $"https://duckduckgo.com/?q=site%3Awikipedia.org+{Uri.EscapeDataString(obj.DisplayName)}&ia=web";
        }

        private void WebBrowser_OnPageLoaded(object sender, PageLoadedEventArgs e) {
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
    }
}
