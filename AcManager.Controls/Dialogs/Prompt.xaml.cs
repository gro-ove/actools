using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Controls.Dialogs {
    public partial class Prompt : IValueConverter {
        private Prompt(string title, string description, string defaultValue, string watermark, string toolTip, bool multiline, bool passwordMode, bool required,
                int maxLength, IEnumerable<string> suggestions) {
            DataContext = new ViewModel(description, defaultValue, watermark, toolTip, required);
            InitializeComponent();
            Buttons = new[] { OkButton, CancelButton };

            if (required) {
                OkButton.SetBinding(IsEnabledProperty, new Binding {
                    Source = DataContext,
                    Path = new PropertyPath(nameof(ViewModel.Text)),
                    Converter = this
                });
            }

            Title = title;
            FrameworkElement element;

            if (passwordMode) {
                var passwordBox = new ProperPasswordBox();
                passwordBox.SetBinding(ProperPasswordBox.PasswordProperty, new Binding {
                    Source = DataContext,
                    Path = new PropertyPath(nameof(ViewModel.Text)),
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                });

                passwordBox.Focus();
                passwordBox.SelectAll();

                element = passwordBox;
            } else if (suggestions != null) {
                var comboBox = new BetterComboBox {
                    IsEditable = true,
                    IsTextSearchEnabled = true,
                    ItemsSource = suggestions.ToList()
                };

                comboBox.SetBinding(ComboBox.TextProperty, new Binding {
                    Source = DataContext,
                    Path = new PropertyPath(nameof(ViewModel.Text)),
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                    ValidatesOnDataErrors = true
                });

                var textBox = comboBox.Template?.FindName(@"PART_EditableTextBox", comboBox) as TextBox;
                if (textBox != null) {
                    textBox.SelectAll();
                    textBox.Focus();
                } else {
                    comboBox.Focus();
                }

                if (watermark != null) {
                    comboBox.Placeholder = watermark;
                }

                element = comboBox;
            } else {
                var textBox = new BetterTextBox();
                textBox.SetBinding(TextBox.TextProperty, new Binding {
                    Source = DataContext,
                    Path = new PropertyPath(nameof(ViewModel.Text)),
                    Mode = BindingMode.TwoWay
                });

                if (maxLength != -1) {
                    textBox.MaxLength = maxLength;
                }

                if (multiline) {
                    textBox.AcceptsReturn = true;
                    textBox.TextWrapping = TextWrapping.Wrap;
                    textBox.Height = 240;
                    textBox.Width = 480;
                }

                if (watermark != null) {
                    textBox.Placeholder = watermark;
                }

                textBox.Focus();
                textBox.SelectAll();
                element = textBox;
            }

            Panel.Children.Add(element);
            if (toolTip != null) element.SetValue(ToolTipProperty, toolTip);

            Closing += (sender, args) => {
                if (MessageBoxResult != MessageBoxResult.OK) return;
                Result = Model.Text;
            };
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged, INotifyDataErrorInfo {
            private string _text;

            public string Text {
                get { return _text; }
                set {
                    if (Equals(value, _text)) return;
                    _text = value;
                    OnPropertyChanged();
                    OnErrorsChanged();
                }
            }

            public string Description { get; }

            public string Watermark { get; }

            public string ToolTip { get; }

            public bool Required { get; }

            public ViewModel(string description, string defaultValue, string watermark, string toolTip, bool required) {
                Description = description;
                Text = defaultValue;
                Watermark = watermark;
                ToolTip = toolTip;
                Required = required;
            }

            public IEnumerable GetErrors(string propertyName) {
                switch (propertyName) {
                    case nameof(Text):
                        return Required && string.IsNullOrWhiteSpace(Text) ? new[] { "Required value" } : null;
                    default:
                        return null;
                }
            }

            public bool HasErrors => Required && string.IsNullOrWhiteSpace(Text);
            public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

            public void OnErrorsChanged([CallerMemberName] string propertyName = null) {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            }
        }

        public async Task ShowAsync(CancellationToken cancellation) {
            await Application.Current.Dispatcher.InvokeAsync(ShowDialog, DispatcherPriority.Normal, cancellation).Task;
            if (cancellation.IsCancellationRequested) Close();
        }

        [CanBeNull]
        public string Result { get; private set; }

        /// <summary>
        /// Shows a message for user to input something, returns result string or null if user cancelled input.
        /// </summary>
        /// <param name="description">Some description, could ends with “:”.</param>
        /// <param name="title">Title, in title casing</param>
        /// <param name="defaultValue">Default value in the input area</param>
        /// <param name="watermark">Some semi-transparent hint in the input area</param>
        /// <param name="toolTip">Tooltip for the input area</param>
        /// <param name="multiline">Is the input area should be multilined.</param>
        /// <param name="passwordMode">Hide inputting value.</param>
        /// <param name="maxLength">Length limitation.</param>
        /// <param name="suggestions">Suggestions if needed.</param>
        /// <returns>Result string or null if user cancelled input.</returns>
        [CanBeNull]
        public static string Show(string description, string title, string defaultValue = "", string watermark = null, string toolTip = null,
                bool multiline = false, bool passwordMode = false, bool required = false, int maxLength = -1, IEnumerable<string> suggestions = null) {
            if (passwordMode && suggestions != null) throw new ArgumentException(@"Can’t have suggestions with password mode");
            if (passwordMode && multiline) throw new ArgumentException(@"Can’t use multiline input area with password mode");
            if (suggestions != null && multiline) throw new ArgumentException(@"Can’t use multiline input area with suggestions");

            var dialog = new Prompt(title, description, defaultValue, watermark, toolTip, multiline, passwordMode, required, maxLength, suggestions);
            dialog.ShowDialog();

            var result = dialog.Result;
            if (maxLength != -1 && result?.Length > maxLength) {
                result = result.Substring(0, maxLength);
            }
            return result;
        }

        /// <summary>
        /// Shows a message for user to input something, returns result string or null if user cancelled input. Async version with cancellation support.
        /// </summary>
        /// <param name="description">Some description, could ends with “:”.</param>
        /// <param name="title">Title, in title casing</param>
        /// <param name="defaultValue">Default value in the input area</param>
        /// <param name="watermark">Some semi-transparent hint in the input area</param>
        /// <param name="toolTip">Tooltip for the input area</param>
        /// <param name="multiline">Is the input area should be multilined.</param>
        /// <param name="passwordMode">Hide inputting value.</param>
        /// <param name="maxLength">Length limitation.</param>
        /// <param name="suggestions">Suggestions if needed.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>Result string or null if user cancelled input.</returns>
        [ItemCanBeNull]
        public static async Task<string> ShowAsync(string description, string title, string defaultValue = "", string watermark = null, string toolTip = null,
                bool multiline = false, bool passwordMode = false, bool required = false, int maxLength = -1, IEnumerable<string> suggestions = null,
                CancellationToken cancellation = default(CancellationToken)) {
            if (passwordMode && suggestions != null) throw new ArgumentException(@"Can’t have suggestions with password mode");
            if (passwordMode && multiline) throw new ArgumentException(@"Can’t use multiline input area with password mode");
            if (suggestions != null && multiline) throw new ArgumentException(@"Can’t use multiline input area with suggestions");

            var dialog = new Prompt(title, description, defaultValue, watermark, toolTip, multiline, passwordMode, required, maxLength, suggestions);
            try {
                await dialog.ShowAsync(cancellation);
            } catch (TaskCanceledException) {
                return null;
            }

            var result = dialog.Result;
            if (maxLength != -1 && result?.Length > maxLength) {
                result = result.Substring(0, maxLength);
            }
            return result;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return !string.IsNullOrWhiteSpace(value?.ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
