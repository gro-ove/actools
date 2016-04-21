using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.About {
    public class PieceOfInformation : Displayable, IWithId {
        private readonly string _keyIsNotNew;

        public sealed override string DisplayName { get; set; }

        public PieceOfInformation(string displayName, string details, string id = null) {
            _keyIsNotNew = "PieceOfInformation.IsNotNew_" + displayName.Length + "_" + displayName.GetHashCode() + "_" + details.GetHashCode();
            Id = id;

            Details = details;
            DisplayName = displayName;
            IsNew = !ValuesStorage.GetBool(_keyIsNotNew);
        }

        public string Details { get; }

        public string Id { get; set; }

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
            ValuesStorage.Set(_keyIsNotNew, true);
        }
    }
}