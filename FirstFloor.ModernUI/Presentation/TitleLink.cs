using System.ComponentModel;
using System.Windows.Markup;

namespace FirstFloor.ModernUI.Presentation {
    [ContentProperty(nameof(Content))]
    public sealed class TitleLink : Link {
        private object _content;

        [Bindable(true)]
        public object Content {
            get => _content;
            set => Apply(value, ref _content);
        }

        public override string DisplayName {
            get => Content?.ToString();
            set => Content = value;
        }

        private string _groupKey;

        public string GroupKey {
            get => _groupKey;
            set => Apply(value, ref _groupKey);
        }

        private bool _isActive;

        public bool IsActive {
            get => _isActive;
            set => Apply(value, ref _isActive);
        }

        private bool _isAccented;

        public bool IsAccented {
            get => _isAccented;
            set => Apply(value, ref _isAccented);
        }
    }
}