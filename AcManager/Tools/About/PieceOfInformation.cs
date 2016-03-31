using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.About {
    public class PieceOfInformation : Displayable {
        public sealed override string DisplayName { get; set; }

        public PieceOfInformation(string displayName, string details) {
            Id = displayName.Length + "_" + displayName.GetHashCode() + "_" + details.GetHashCode();

            Details = details;
            DisplayName = displayName;
            IsNew = !ValuesStorage.GetBool(KeyIsNotNew);
        }

        public string Key { get; set; }

        public string Details { get; }

        public string Id { get; }

        private bool _isNew;

        public bool IsNew {
            get { return _isNew; }
            private set {
                if (Equals(value, _isNew)) return;
                _isNew = value;
                OnPropertyChanged();
            }
        }

        private string KeyIsNotNew => "PieceOfInformation.IsNotNew_" + Id;

        public void MarkAsRead() {
            IsNew = false;
            AboutHelper.Instance.CheckIfThereIsSomethingNew();
            ValuesStorage.Set(KeyIsNotNew, true);
        }
    }
}