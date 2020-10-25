using System;
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
                    CloseCommand.Execute();
                    return;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(Source));
            }
        }

        public override Uri Source => _baseUri.AddQueryParam("Filter", _value);

        private DelegateCommand _closeCommand;

        public DelegateCommand CloseCommand => _closeCommand ?? (_closeCommand = new DelegateCommand(
                () => Close?.Invoke(this, new LinkCloseEventArgs(LinkCloseMode.Regular))));

        private DelegateCommand _closeAllCommand;

        public DelegateCommand CloseAllCommand => _closeAllCommand ?? (_closeAllCommand = new DelegateCommand(
                () => Close?.Invoke(this, new LinkCloseEventArgs(LinkCloseMode.CloseAll))));

        private DelegateCommand _closeOthersCommand;

        public DelegateCommand CloseOthersCommand => _closeOthersCommand ?? (_closeOthersCommand = new DelegateCommand(
                () => Close?.Invoke(this, new LinkCloseEventArgs(LinkCloseMode.CloseOthers))));

        private DelegateCommand _closeToRightCommand;

        public DelegateCommand CloseToRightCommand => _closeToRightCommand ?? (_closeToRightCommand = new DelegateCommand(
                () => Close?.Invoke(this, new LinkCloseEventArgs(LinkCloseMode.CloseToRight))));

        public event EventHandler<LinkCloseEventArgs> Close;

        public const string DraggableFormat = "X-Search-Tab";

        string IDraggable.DraggableFormat => DraggableFormat;
    }
}