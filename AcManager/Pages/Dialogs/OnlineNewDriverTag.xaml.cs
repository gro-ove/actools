using System;
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using AcManager.Tools.Managers.Online;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Pages.Dialogs {
    public partial class OnlineNewDriverTag {
        private ViewModel Model => (ViewModel)DataContext;

        public OnlineNewDriverTag(ServerEntry.CurrentDriver driver = null) {
            DataContext = new ViewModel(driver);
            InitializeComponent();
            Buttons = new [] { OkButton, CancelButton };
        }

        public class ViewModel : NotifyPropertyChanged, INotifyDataErrorInfo {
            [CanBeNull]
            public ServerEntry.CurrentDriver Driver { get; }

            public ViewModel([CanBeNull] ServerEntry.CurrentDriver driver) {
                Driver = driver;
            }

            private string _tagName;

            public string TagName {
                get { return _tagName; }
                set {
                    if (Equals(value, _tagName)) return;
                    var oldValue = _tagName;
                    _tagName = value;
                    OnPropertyChanged();

                    if (string.IsNullOrWhiteSpace(oldValue) || string.IsNullOrWhiteSpace(value)) {
                        OnErrorsChanged();
                    }
                }
            }

            private Color _tagColor = Colors.White;

            public Color TagColor {
                get { return _tagColor; }
                set => Apply(value, ref _tagColor);
            }

            public IEnumerable GetErrors(string propertyName) {
                switch (propertyName) {
                    case nameof(TagName):
                        return string.IsNullOrWhiteSpace(TagName) ? new[] { "Required value" } : null;
                    default:
                        return null;
                }
            }

            public bool HasErrors => string.IsNullOrWhiteSpace(TagName);
            public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

            public void OnErrorsChanged([CallerMemberName] string propertyName = null) {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            }
        }

        private void OnClosing(object sender, CancelEventArgs e) {
            if (!IsResultOk) return;

            var tagId = ServerEntry.DriverTag.CreateTag(Model.TagName, Model.TagColor).Id;
            Model.Driver?.AddTagCommand.Execute(tagId);
        }
    }
}