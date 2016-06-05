using System.Windows;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Controls.Pages.Dialogs {
    public partial class Prompt {
        private Prompt(string title, string description, string defaultValue, string watermark, string toolTip, bool multiline, bool passwordMode) {
            DataContext = new PromptViewModel(description, defaultValue, watermark, toolTip);
            InitializeComponent();
            Buttons = new[] { OkButton, CancelButton };

            Title = title;

            if (passwordMode) {
                PasswordBox.Focus();
                PasswordBox.SelectAll();
                PasswordBox.Visibility = Visibility.Visible;
                TextBox.Visibility = Visibility.Collapsed;
            } else {
                TextBox.Focus();
                TextBox.SelectAll();

                if (multiline) {
                    TextBox.AcceptsReturn = true;
                    TextBox.TextWrapping = TextWrapping.Wrap;
                    TextBox.Height = 240;
                    TextBox.Width = 480;
                }
            }

            Closing += (sender, args) => {
                if (MessageBoxResult != MessageBoxResult.OK) return;
                Result = Model.Text;
            };
        }

        private PromptViewModel Model => (PromptViewModel)DataContext;

        public class PromptViewModel : NotifyPropertyChanged {
            private string _text;

            public string Text {
                get { return _text; }
                set {
                    if (Equals(value, _text)) return;
                    _text = value;
                    OnPropertyChanged();
                }
            }

            private string _description;

            public string Description {
                get { return _description; }
                set {
                    if (Equals(value, _description)) return;
                    _description = value;
                    OnPropertyChanged();
                }
            }

            private string _watermark;

            public string Watermark {
                get { return _watermark; }
                set {
                    if (Equals(value, _watermark)) return;
                    _watermark = value;
                    OnPropertyChanged();
                }
            }

            private string _toolTip;

            public string ToolTip {
                get { return _toolTip; }
                set {
                    if (Equals(value, _toolTip)) return;
                    _toolTip = value;
                    OnPropertyChanged();
                }
            }

            public PromptViewModel(string description, string defaultValue, string watermark, string toolTip) {
                Description = description;
                Text = defaultValue;
                Watermark = watermark;
                ToolTip = toolTip;
            }
        }

        [CanBeNull]
        public string Result { get; private set; }

        [CanBeNull]
        public static string Show(string description, string title, string defaultValue = "", string watermark = null, string toolTip = null,
            bool multiline = false, bool passwordMode = false, int maxLength = -1) {
            var dialog = new Prompt(title, description, defaultValue, watermark, toolTip, multiline, passwordMode);
            dialog.ShowDialog();

            if (maxLength != -1) {
                dialog.TextBox.MaxLength = maxLength;
            }

            var result = dialog.Result;
            if (maxLength != -1 && result?.Length > maxLength) {
                result = result.Substring(0, maxLength);
            }
            return result;
        }
    }
}
