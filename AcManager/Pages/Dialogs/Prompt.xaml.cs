using System.Windows;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Dialogs {
    public partial class Prompt {
        private Prompt(string title, string description, string defaultValue, string watermark, string toolTip, bool multiline) {
            DataContext = new PromptViewModel(description, defaultValue, watermark, toolTip);
            InitializeComponent();
            Buttons = new[] { OkButton, CancelButton };

            Title = title;
            TextBox.Focus();
            TextBox.SelectAll();

            if (multiline) {
                TextBox.AcceptsReturn = true;
                TextBox.TextWrapping = TextWrapping.Wrap;
                TextBox.Height = 240;
                TextBox.Width = 480;
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

        public string Result { get; private set; }

        public static string Show(string title, string description, string defaultValue = "", string watermark = null, string toolTip = null,
                bool multiline = false) {
            var dialog = new Prompt(title, description, defaultValue, watermark, toolTip, multiline);
            dialog.ShowDialog();
            return dialog.Result;
        }
    }
}
