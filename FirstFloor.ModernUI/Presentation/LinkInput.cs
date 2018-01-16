using System;
using System.Windows.Input;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;

namespace FirstFloor.ModernUI.Presentation {
    public class LinkInput : Link, IDraggable {
        public LinkInput(Uri baseUri, string value) {
            _baseUri = baseUri;
            _value = value;
        }

        private string _value;
        private readonly Uri _baseUri;

        internal string PreviousValue;

        public override string DisplayName {
            get => _value;
            set {
                value = value.Trim();
                if (_value == value) return;

                PreviousValue = _value;
                _value = value;

                if (value == "") {
                    CloseCommand.Execute(null);
                    return;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(Source));
            }
        }

        public override Uri Source => _baseUri.AddQueryParam("Filter", _value);

        private ICommand _closeCommand;

        public ICommand CloseCommand => _closeCommand ?? (_closeCommand = new DelegateCommand(() => {
            Close?.Invoke(this, EventArgs.Empty);
        }));

        public event EventHandler Close;

        public const string DraggableFormat = "X-Search-Tab";

        string IDraggable.DraggableFormat => DraggableFormat;
    }
}