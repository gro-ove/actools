using System.Linq;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class DriverModelObject : AcCommonSingleFileObject {
        public const string FileExtension = ".kn5";

        public override string Extension => FileExtension;

        public DriverModelObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
            AcId = id.ApartFromLast(FileExtension);

            _usingsCarsIds = ValuesStorage.GetStringList(KeyUsingsCarsIds).ToArray();
            IsUsed = _usingsCarsIds.Any();
        }

        public override bool HasData => true;

        private string KeyUsingsCarsIds => @"__tmp_DriverModelObject.UsingsCarsIds_" + Id;

        [NotNull]
        private string[] _usingsCarsIds;

        [NotNull]
        public string[] UsingsCarsIds {
            get => _usingsCarsIds;
            set {
                if (Equals(value, _usingsCarsIds)) return;
                _usingsCarsIds = value;
                OnPropertyChanged();

                IsUsed = value.Any();
                ValuesStorage.Set(KeyUsingsCarsIds, value);
            }
        }

        private bool _isUsed;

        public bool IsUsed {
            get => _isUsed;
            set {
                if (Equals(value, _isUsed)) return;
                _isUsed = value;
                OnPropertyChanged();

                ErrorIf(IsUsed && !Enabled, AcErrorType.Font_UsedButDisabled);
            }
        }

        public string AcId { get; }
    }
}