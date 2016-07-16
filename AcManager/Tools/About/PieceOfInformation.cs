using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.About {
    public sealed class PieceOfInformation : Displayable, IWithId {
        private readonly string _sid;

        public PieceOfInformation(string sid, string id, string displayName, string version, string content, bool limited, bool hidden) {
            _sid = "PieceOfInformation.IsNotNew_" + sid;
            Id = id;

            DisplayName = displayName;
            Content = content;
            Version = version;
            IsLimited = limited;

            IsNew = !ValuesStorage.GetBool(_sid);
            IsHidden = IsNew && hidden;
        }

        public string Id { get; }

        public string Version { get; }

        public string Content { get; }

        public bool IsLimited { get; }

        public bool IsHidden { get; }

        private bool _isNew;

        public bool IsNew {
            get { return _isNew; }
            private set {
                if (Equals(value, _isNew)) return;
                _isNew = value;
                OnPropertyChanged();
            }
        }

        public void MarkAsRead() {
            IsNew = false;
            AboutHelper.Instance.CheckIfThereIsSomethingNew();
            ValuesStorage.Set(_sid, true);
        }
    }
}